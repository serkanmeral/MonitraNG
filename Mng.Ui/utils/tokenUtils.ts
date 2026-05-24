import { decodeJwt } from 'jose';

/**
 * Cookie Secure bayrağı: yalnızca HTTPS'te true.
 * Production build + HTTP (ör. Odak :3000) iken NODE_ENV=production ile Secure cookie
 * tarayıcıda kaydedilmez → "Access token bulunamadı" hatası.
 */
export function shouldUseSecureCookie(): boolean {
  if (import.meta.client && typeof window !== 'undefined') {
    return window.location.protocol === 'https:';
  }
  return false;
}

/**
 * JWT token'ın expire olup olmadığını kontrol eder
 * @param token JWT token string
 * @param bufferSeconds Expire olmadan kaç saniye önce expire sayılacağı (default: 60)
 * @returns true if token is expired or will expire soon
 */
export function isTokenExpired(token: string | null | undefined, bufferSeconds: number = 60): boolean {
  if (!token) {
    return true;
  }

  try {
    const decoded = decodeJwt(token);
    
    if (!decoded.exp) {
      // Exp claim yoksa expire sayılır
      return true;
    }

    // Expire zamanı (Unix timestamp)
    const expirationTime = decoded.exp;
    
    // Şu anki zaman (Unix timestamp)
    const currentTime = Math.floor(Date.now() / 1000);
    
    // Buffer süresi kadar önce expire sayılır (token yenileme için zaman tanır)
    return currentTime >= (expirationTime - bufferSeconds);
  } catch (error) {
    // Decode hatası varsa expire sayılır
    console.error('Token decode error:', error);
    return true;
  }
}

/**
 * Token'ın expire zamanını döndürür
 * @param token JWT token string
 * @returns Expiration time as Date or null if invalid
 */
export function getTokenExpiration(token: string | null | undefined): Date | null {
  if (!token) {
    return null;
  }

  try {
    const decoded = decodeJwt(token);
    
    if (!decoded.exp) {
      return null;
    }

    return new Date(decoded.exp * 1000);
  } catch (error) {
    console.error('Token decode error:', error);
    return null;
  }
}

