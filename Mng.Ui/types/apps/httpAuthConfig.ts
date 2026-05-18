/**
 * HTTP Auth Config — Token endpoint tanımları.
 * mon_http_auth_configs dataset kaydı.
 * HTTP Collector'da Bearer token auth için merkezi tanım.
 */

export interface MonHttpAuthConfig {
  __dataId: string;
  name: string;
  tokenUrl: string;
  tokenMethod: 'GET' | 'POST';
  tokenBodyType: 'json' | 'form';
  /** JSON: object, Form: Record<string, string> */
  tokenBody: Record<string, unknown>;
  tokenResponsePath: string;
  description?: string | null;
}
