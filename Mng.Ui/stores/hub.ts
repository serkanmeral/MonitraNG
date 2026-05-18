import { defineStore } from 'pinia';
import { HubConnection, HubConnectionBuilder, LogLevel, HttpTransportType } from '@microsoft/signalr';
import { useAuthStore } from '@/stores/auth';

/**
 * Hub Message Data Type
 */
export interface HubMessage {
  routingKey: string;
  message: any;
  timestamp: string;
}

/**
 * Subscription Options
 */
export interface SubscriptionOptions {
  /**
   * Filter function: returns true if message should be delivered to handler
   */
  filter: (data: HubMessage) => boolean;
  /**
   * Handler function: called when a filtered message is received
   */
  handler: (data: HubMessage) => void;
}

/**
 * Subscription Entry
 */
interface Subscription {
  id: string;
  filter: (data: HubMessage) => boolean;
  handler: (data: HubMessage) => void;
}

/**
 * Shared SignalR Hub Connection Store with Subscription Pattern
 * 
 * This store manages a single SignalR Hub connection that can be shared across
 * the entire application. Components subscribe to messages using unique subscription IDs
 * with filters to receive only relevant messages.
 */
interface ReconnectHandlerEntry {
  id: string;
  handler: () => void | Promise<void>;
}

interface HubState {
  hubConnection: HubConnection | null;
  isConnected: boolean;
  isConnecting: boolean;
  connectionError: string | null;
  connectionPromise: Promise<void> | null;
  subscriptions: Map<string, Subscription>;
  internalHandler: ((data: HubMessage) => void) | null;
  lastMessageCache: Map<string, number>; // routingKey -> timestamp (deduplication için)
  /** SignalR otomatik yeniden bağlanınca çalıştırılır (sohbet geçmişi DG ile doldurulur). */
  reconnectHandlers: ReconnectHandlerEntry[];
}

export const useHubStore = defineStore('hub', {
  state: (): HubState => ({
    hubConnection: null,
    isConnected: false,
    isConnecting: false,
    connectionError: null,
    connectionPromise: null,
    subscriptions: new Map(),
    internalHandler: null,
    lastMessageCache: new Map(), // Deduplication için
  }),

  getters: {
    connection: (state) => state.hubConnection,
    connected: (state) => state.isConnected,
    connecting: (state) => state.isConnecting,
    error: (state) => state.connectionError,
    subscriptionCount: (state) => state.subscriptions.size,
  },

  actions: {
    /**
     * Connect to SignalR Hub (shared connection)
     */
    async connectToHub() {
      // Client-side only
      if (process.server) return;

      // Zaten bağlıysa tekrar bağlanma
      if (this.hubConnection?.state === 'Connected') {
        // Bağlantı zaten var, ama handler yoksa ekle (güvenlik için)
        if (!this.internalHandler && this.hubConnection) {
          // Internal handler oluştur ve ekle
          this.internalHandler = (data: HubMessage) => {
            const subscriptionCount = this.subscriptions.size;
            const subscriptionIds = Array.from(this.subscriptions.keys());
            
            if (subscriptionCount > 0 && import.meta.dev) {
              console.log('[Hub] Message received', {
                subscriptionCount,
                subscriptionIds,
                routingKey: data.routingKey?.substring(0, 50)
              });
            }
            
            this.subscriptions.forEach((subscription) => {
              try {
                if (subscription.filter(data)) {
                  if (import.meta.dev) console.log('[Hub] Calling handler for subscription', subscription.id);
                  subscription.handler(data);
                }
              } catch (error) {
                console.error('[Hub Store] Error in subscription handler', {
                  subscriptionId: subscription.id,
                  error
                });
              }
            });
          };
          
          // Önce eski handler'ı kaldır (güvenlik için)
          try {
            this.hubConnection.off('ReceiveMessage', this.internalHandler);
          } catch (error) {
            // Handler yoksa hata vermez
          }
          this.hubConnection.on('ReceiveMessage', this.internalHandler);
          if (import.meta.dev) console.log('[Hub Store] Internal handler registered to existing connection');
        }
        return;
      }

      // Bağlantı kuruluyorsa, mevcut promise'i bekle
      if (this.connectionPromise) {
        await this.connectionPromise;
        return;
      }

      // Eski bağlantıyı temizle
      if (this.hubConnection) {
        // Eski handler'ı kaldır (eğer varsa)
        if (this.internalHandler) {
          try {
            this.hubConnection.off('ReceiveMessage', this.internalHandler);
          } catch (error) {
            // Hata önemli değil
          }
        }
        try {
          await this.hubConnection.stop();
        } catch (error) {
          // Hata önemli değil
        }
        this.hubConnection = null;
        this.internalHandler = null;
      }

      const authStore = useAuthStore();
      const config = useRuntimeConfig();

      this.isConnecting = true;
      this.connectionError = null;

      // Connection promise oluştur (race condition'ı önlemek için)
      this.connectionPromise = (async () => {
        try {
          const token = authStore.accessToken;
          if (!token) {
            throw new Error('Access token bulunamadı. Lütfen tekrar giriş yapın.');
          }

          // Hub URL belirleme:
          // - HUB_URL varsa direkt Hub'a bağlan (development için)
          // - HUB_URL yoksa Gateway üzerinden bağlan (production için - Gateway her zaman dolu)
          let hubBaseUrl: string;
          
          if (config.public.hubUrl) {
            // Hub URL varsa direkt Hub'a bağlan (development için HTTP, port 5020)
            hubBaseUrl = config.public.hubUrl;
          } else {
            // Hub URL yoksa Gateway üzerinden bağlan (production için HTTPS)
            hubBaseUrl = `${config.public.gatewayUrl}/hub`;
          }
          
          const connectionUrl = `${hubBaseUrl}/ws?access_token=${encodeURIComponent(token)}`;

          const hubConnection = new HubConnectionBuilder()
            .withUrl(connectionUrl, {
              skipNegotiation: false,
              transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling
            })
            .withAutomaticReconnect({
              nextRetryDelayInMilliseconds: (retryContext) => {
                if (retryContext.previousRetryCount < 3) {
                  return 2000; // 2 saniye
                }
                return 5000; // 5 saniye
              },
            })
            .withServerTimeout(60000) // 60 saniye server timeout (default 30 saniye)
            .withKeepAliveInterval(15000) // 15 saniye keep-alive (default 15 saniye)
            .configureLogging(process.env.NODE_ENV === 'development' ? LogLevel.Warning : LogLevel.Error)
            .build();

          // Connection state handlers
          hubConnection.onreconnecting(() => {
            this.isConnecting = true;
          });

          hubConnection.onreconnected(async () => {
            this.isConnecting = false;
            this.isConnected = true;
            this.connectionError = null;
            await this.dispatchReconnectHandlers();
          });

          hubConnection.onclose((error) => {
            this.isConnected = false;
            
            // Timeout hatalarını sessizce handle et (sürekli console error'ları engellemek için)
            if (error) {
              const errorMessage = error.message || '';
              const isTimeoutError = errorMessage.includes('timeout') || 
                                    errorMessage.includes('Server timeout') ||
                                    errorMessage.includes('elapsed without receiving');
              
              if (isTimeoutError) {
                // Timeout hatalarını sessizce handle et (console'a yazma)
                // Bu hatalar genellikle geçici network sorunları veya sunucu yanıt vermeme durumlarıdır
                // Automatic reconnect mekanizması zaten bu durumu handle ediyor
                this.connectionError = null; // Timeout hatalarını state'de saklama
                return;
              }
              
              // Diğer hatalar için normal işlem
              this.connectionError = `Bağlantı kapatıldı: ${error.message}`;
              
              // Sadece gerçek bağlantı hatalarını console'a yaz (timeout değilse)
              if (process.env.NODE_ENV === 'development') {
                console.warn('[Hub Store] Connection closed', {
                  message: error.message,
                  error: error.name
                });
              }
            }
          });

          // Internal handler: Tüm mesajları alır ve subscription'lara dağıtır
          this.internalHandler = (data: HubMessage) => {
            // Deduplication: Aynı mesajı 2 kez işleme
            // Mesaj ID'si varsa onu kullan, yoksa routingKey + timestamp kombinasyonunu kullan
            let messageKey: string;
            if (data.message && typeof data.message === 'object' && data.message.id) {
              // Mesaj ID'si varsa onu kullan (en güvenilir yöntem)
              messageKey = `id_${data.message.id}`;
            } else {
              // Mesaj ID'si yoksa routingKey + timestamp kombinasyonunu kullan
              messageKey = `${data.routingKey}_${data.timestamp}`;
            }
            
            const now = Date.now();
            const lastTimestamp = this.lastMessageCache.get(messageKey) || 0;
            const dedupeWindow = 2000; // 2 saniye içinde aynı mesaj tekrar gelirse ignore et
            
            if (now - lastTimestamp < dedupeWindow) {
              if (import.meta.dev) console.log('[Hub] Duplicate message ignored', {
                messageKey: messageKey.substring(0, 80),
                routingKey: data.routingKey?.substring(0, 50),
                timestamp: data.timestamp,
                timeSinceLast: now - lastTimestamp,
                messageId: data.message?.id || 'N/A'
              });
              return; // Duplicate mesajı ignore et
            }
            
            // Mesajı cache'e kaydet
            this.lastMessageCache.set(messageKey, now);
            
            // Cache'i temizle (eski mesajları kaldır - memory leak önlemek için)
            if (this.lastMessageCache.size > 100) {
              const entries = Array.from(this.lastMessageCache.entries());
              const toRemove = entries.filter(([_, ts]) => now - ts > 10000); // 10 saniyeden eski olanları kaldır
              toRemove.forEach(([key]) => this.lastMessageCache.delete(key));
            }
            
            if (import.meta.dev) {
              console.log('[Hub] Processing message', {
                messageKey: messageKey.substring(0, 80),
                messageId: data.message?.id || 'N/A',
                routingKey: data.routingKey?.substring(0, 50)
              });
            }
            
            const subscriptionCount = this.subscriptions.size;
            const subscriptionIds = Array.from(this.subscriptions.keys());
            
            if (subscriptionCount > 0 && import.meta.dev) {
              console.log('[Hub] Message received', {
                subscriptionCount,
                subscriptionIds,
                routingKey: data.routingKey?.substring(0, 50)
              });
            }
            
            // Tüm subscription'lara mesajı dağıt
            this.subscriptions.forEach((subscription) => {
              try {
                if (subscription.filter(data)) {
                  if (import.meta.dev) console.log('[Hub] Calling handler for subscription', subscription.id);
                  // Handler'ı çağır
                  subscription.handler(data);
                }
              } catch (error) {
                console.error('[Hub Store] Error in subscription handler', {
                  subscriptionId: subscription.id,
                  error
                });
              }
            });
          };

          // SignalR handler'ı ekle (tek bir handler)
          // Önce eski handler'ı kaldır (eğer varsa - güvenlik için)
          try {
            hubConnection.off('ReceiveMessage', this.internalHandler);
          } catch (error) {
            // Handler yoksa hata vermez, devam et
          }
          hubConnection.on('ReceiveMessage', this.internalHandler);
          if (import.meta.dev) console.log('[Hub Store] Internal handler registered to SignalR');

          await hubConnection.start();
          this.hubConnection = hubConnection;
          this.isConnected = true;
          this.connectionError = null;
        } catch (error: any) {
          this.connectionError = error.message || 'Bağlantı hatası oluştu.';
          this.isConnected = false;
          this.internalHandler = null;
          throw error;
        } finally {
          this.isConnecting = false;
          this.connectionPromise = null;
        }
      })();

      await this.connectionPromise;
    },

    /**
     * Disconnect from SignalR Hub
     */
    async disconnectFromHub() {
      if (this.hubConnection) {
        try {
          // Internal handler'ı kaldır
          if (this.internalHandler) {
            this.hubConnection.off('ReceiveMessage', this.internalHandler);
            if (import.meta.dev) console.log('[Hub Store] Internal handler removed from SignalR');
            this.internalHandler = null;
          }
          
          await this.hubConnection.stop();
          this.hubConnection = null;
          this.isConnected = false;
          this.connectionError = null;
          this.subscriptions.clear();
          this.reconnectHandlers = [];
          this.connectionPromise = null;
          this.lastMessageCache.clear();
        } catch (error) {
          // Hata önemli değil, sessizce devam et
        }
      }
    },

    /**
     * Subscribe to messages with a filter
     * @param subscriptionId Unique identifier for this subscription (prevents duplicates)
     * @param options Subscription options (filter and handler)
     * @returns true if subscription was added, false if already exists
     */
    subscribe(subscriptionId: string, options: SubscriptionOptions): boolean {
      if (this.subscriptions.has(subscriptionId)) {
        if (import.meta.dev) {
          console.warn('[Hub Store] Subscription already exists, skipping', {
            subscriptionId,
            subscriptionCount: this.subscriptions.size,
            existingSubscriptionIds: Array.from(this.subscriptions.keys())
          });
        }
        return false;
      }

      if (import.meta.dev) {
        console.log('[Hub Store] Adding subscription', {
          subscriptionId,
          subscriptionCount: this.subscriptions.size + 1
        });
      }

      // Subscription'ı ekle
      this.subscriptions.set(subscriptionId, {
        id: subscriptionId,
        filter: options.filter,
        handler: options.handler,
      });

      return true;
    },

    /**
     * Unsubscribe from messages
     * @param subscriptionId Unique identifier for the subscription to remove
     * @returns true if subscription was removed, false if not found
     */
    unsubscribe(subscriptionId: string): boolean {
      if (!this.subscriptions.has(subscriptionId)) {
        if (import.meta.dev) {
          console.warn('[Hub Store] Subscription not found, cannot remove', {
            subscriptionId,
            subscriptionCount: this.subscriptions.size,
            existingSubscriptionIds: Array.from(this.subscriptions.keys())
          });
        }
        return false;
      }

      this.subscriptions.delete(subscriptionId);
      return true;
    },

    /**
     * Check if a subscription exists
     * @param subscriptionId Unique identifier for the subscription
     */
    hasSubscription(subscriptionId: string): boolean {
      return this.subscriptions.has(subscriptionId);
    },

    registerReconnectHandler(id: string, handler: () => void | Promise<void>) {
      this.unregisterReconnectHandler(id);
      this.reconnectHandlers.push({ id, handler });
    },

    unregisterReconnectHandler(id: string) {
      const idx = this.reconnectHandlers.findIndex((h) => h.id === id);
      if (idx >= 0) this.reconnectHandlers.splice(idx, 1);
    },

    async dispatchReconnectHandlers() {
      const list = [...this.reconnectHandlers];
      for (const { id, handler } of list) {
        try {
          await Promise.resolve(handler());
        } catch (e) {
          console.error('[Hub Store] reconnect handler error', id, e);
        }
      }
    },
  },
});
