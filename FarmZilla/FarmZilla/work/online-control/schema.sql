CREATE TABLE IF NOT EXISTS devices (
 code TEXT PRIMARY KEY,
 hardware TEXT NOT NULL,
 public_jwk TEXT NOT NULL,
 name TEXT NOT NULL DEFAULT '',
 status TEXT NOT NULL DEFAULT 'pending' CHECK(status IN ('pending','allowed','blocked')),
 created_at INTEGER NOT NULL,
 updated_at INTEGER NOT NULL,
 last_seen INTEGER,
 place TEXT,
 features TEXT NOT NULL DEFAULT 'controle,rota',
 log_enabled INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS challenges (
 nonce TEXT PRIMARY KEY,
 device_code TEXT NOT NULL REFERENCES devices(code),
 expires_at INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS challenges_expiry ON challenges(expires_at);
CREATE TABLE IF NOT EXISTS audit (
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 device_code TEXT NOT NULL,
 action TEXT NOT NULL,
 at INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS logs (
 id INTEGER PRIMARY KEY AUTOINCREMENT,
 device_code TEXT NOT NULL,
 at INTEGER NOT NULL,
 version TEXT NOT NULL DEFAULT '',
 line TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS logs_device ON logs(device_code,id);
CREATE INDEX IF NOT EXISTS logs_at ON logs(at);
CREATE TABLE IF NOT EXISTS admin_sessions (
 id_hash TEXT PRIMARY KEY,
 pin_hash TEXT NOT NULL,
 created_at INTEGER NOT NULL,
 expires_at INTEGER NOT NULL,
 failed INTEGER NOT NULL DEFAULT 0,
 access_hash TEXT,
 access_expires INTEGER
);
CREATE INDEX IF NOT EXISTS admin_sessions_access ON admin_sessions(access_hash);
