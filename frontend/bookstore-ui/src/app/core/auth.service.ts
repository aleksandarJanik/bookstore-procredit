import { Injectable, signal } from '@angular/core';
import Keycloak from 'keycloak-js';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly keycloak = new Keycloak({
    url: environment.keycloakUrl,
    realm: 'bookstore',
    clientId: 'bookstore-ui',
  });

  readonly authenticated = signal(false);
  readonly username = signal<string | null>(null);
  readonly unavailable = signal(false);

  async init(): Promise<void> {
    try {
      const authenticated = await this.keycloak.init({
        onLoad: 'check-sso',
        flow: 'standard',
        pkceMethod: 'S256',
        scope: 'books.search',
        checkLoginIframe: false,
      });
      this.authenticated.set(authenticated);
      this.username.set(this.keycloak.tokenParsed?.['preferred_username'] ?? null);
    } catch {
      this.unavailable.set(true);
    }
  }

  login(): Promise<void> {
    return this.keycloak.login({ redirectUri: window.location.origin });
  }

  logout(): Promise<void> {
    return this.keycloak.logout({ redirectUri: window.location.origin });
  }

  async token(): Promise<string | null> {
    if (!this.keycloak.authenticated) {
      return null;
    }

    await this.keycloak.updateToken(30);
    return this.keycloak.token ?? null;
  }
}
