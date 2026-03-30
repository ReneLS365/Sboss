ALTER TABLE inventory_items
  ADD COLUMN IF NOT EXISTS total_integrity_bps BIGINT;

UPDATE inventory_items
SET total_integrity_bps = GREATEST(quantity, 0)::BIGINT * 10000
WHERE total_integrity_bps IS NULL;

ALTER TABLE inventory_items
  ALTER COLUMN total_integrity_bps SET DEFAULT 0,
  ALTER COLUMN total_integrity_bps SET NOT NULL;

ALTER TABLE inventory_items
  ADD CONSTRAINT inventory_items_total_integrity_bps_non_negative CHECK (total_integrity_bps >= 0);
