-- 0.38.0: tools per PC and log only when the owner turns it on. Run once on the remote D1 before deploying the Worker.
ALTER TABLE devices ADD COLUMN features TEXT NOT NULL DEFAULT 'controle,rota';
ALTER TABLE devices ADD COLUMN log_enabled INTEGER NOT NULL DEFAULT 0;
