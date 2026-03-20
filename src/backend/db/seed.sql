INSERT INTO accounts (account_id, external_ref, created_at, updated_at, version)
VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'phase0-account', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', 1)
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO player_profiles (player_profile_id, account_id, display_name, experience, level, created_at, updated_at, version)
VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'Phase0Player', 0, 1, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', 1)
ON CONFLICT (player_profile_id) DO NOTHING;

INSERT INTO seasons (season_id, name, starts_at, ends_at, is_active, created_at, updated_at, version)
VALUES ('cccccccc-cccc-cccc-cccc-cccccccccccc', 'Phase0-Season', '2026-01-01T00:00:00Z', '2026-12-31T23:59:59Z', TRUE, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', 1)
ON CONFLICT (season_id) DO NOTHING;

INSERT INTO level_seeds (level_seed_id, seed_value, biome, template, objective, modifiers_json, par_time_ms, gold_time_ms, version, created_at, updated_at)
VALUES ('dddddddd-dddd-dddd-dddd-dddddddddddd', 'SBOSS-SEED-001', 'urban', 'template_alpha', 'reach_target', '{"modifiers":["none"]}', 120000, 90000, 1, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z')
ON CONFLICT (level_seed_id) DO NOTHING;

INSERT INTO inventory_items (inventory_item_id, account_id, item_code, quantity, created_at, updated_at, version)
VALUES ('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'starter_token', 1, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', 1)
ON CONFLICT (inventory_item_id) DO NOTHING;

INSERT INTO cosmetic_unlocks (cosmetic_unlock_id, account_id, cosmetic_code, unlocked_at, created_at, updated_at, version)
VALUES ('ffffffff-ffff-ffff-ffff-ffffffffffff', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'base_cap', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', 1)
ON CONFLICT (cosmetic_unlock_id) DO NOTHING;

INSERT INTO account_balances (account_id, currency_code, balance, created_at, updated_at, version)
VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'COIN', 100, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', 1)
ON CONFLICT (account_id, currency_code) DO NOTHING;

INSERT INTO economy_transactions (economy_transaction_id, account_id, currency_code, idempotency_key, amount_delta, resulting_balance, resulting_balance_version, reason, created_at, version)
VALUES ('98989898-9898-9898-9898-989898989898', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'COIN', 'seed-opening-balance', 100, 100, 1, 'seed_opening_balance', '2026-01-01T00:00:00Z', 1)
ON CONFLICT (economy_transaction_id) DO NOTHING;

INSERT INTO match_results (match_result_id, account_id, season_id, level_seed_id, score, clear_time_ms, combo_max, penalties, validation_status, created_at, updated_at, version)
VALUES ('12121212-1212-1212-1212-121212121212', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 1000, 110000, 12, 0, 'accepted', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', 1)
ON CONFLICT (match_result_id) DO NOTHING;
