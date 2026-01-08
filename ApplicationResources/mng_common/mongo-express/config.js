/* eslint-env node */
module.exports = {
  mongodb: {
    connectionString: process.env.ME_CONFIG_MONGODB_URL || 'mongodb://admin:admin123@mongo:27017/',
    adminUsername: process.env.ME_CONFIG_MONGODB_ADMINUSERNAME || 'admin',
    adminPassword: process.env.ME_CONFIG_MONGODB_ADMINPASSWORD || 'admin123',
  },
  // Basic Auth devre dışı (geçici - 7 Ocak 2026)
  basicAuth: {
    username: null,
    password: null,
  },
  options: {
    documentsPerPage: 10,
    editorTheme: 'rubyblue',
    collapsibleJSON: true,
    collapsibleJSONDefaultUnfold: 1,
  },
  site: {
    baseUrl: process.env.ME_CONFIG_SITE_BASEURL || '/',
    cookieSecretName: 'mongo-express-session',
    cookieSecret: process.env.ME_CONFIG_SITE_COOKIESECRET || 'cookiesecret',
    host: process.env.VCAP_APP_HOST || 'localhost',
    port: process.env.VCAP_APP_PORT || 8081,
    requestSizeLimit: process.env.ME_CONFIG_REQUEST_SIZE || '50mb',
    sessionSecret: process.env.ME_CONFIG_SITE_SESSIONSECRET || 'sessionsecret',
    sslCert: process.env.ME_CONFIG_SITE_SSL_CRT_PATH || '',
    sslEnabled: process.env.ME_CONFIG_SITE_SSL_ENABLED || false,
    sslKey: process.env.ME_CONFIG_SITE_SSL_KEY_PATH || '',
  },
  useBasicAuth: false, // Basic Auth tamamen devre dışı
};

