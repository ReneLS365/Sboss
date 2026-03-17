INSERT INTO accounts (account_id, external_ref)
VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'phase0-account')
ON CONFLICT (account_id) DO NOTHING;

INSERT INTO player_profiles (player_profile_id, account_id, display_name, experience, level)
VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'Phase0Player', 0, 1)
ON CONFLICT (player_profile_id) DO NOTHING;

INSERT INTO seasons (season_id, name, starts_at, ends_at, is_active)
VALUES ('cccccccc-cccc-cccc-cccc-cccccccccccc', 'Phase0-Season', NOW() - INTERVAL '1 day', NOW() + INTERVAL '30 days', TRUE)
ON CONFLICT (season_id) DO NOTHING;

INSERT INTO level_seeds (level_seed_id, seed_value, biome, template, objective, modifiers_json, par_time_ms, gold_time_ms, version)
VALUES ('dddddddd-dddd-dddd-dddd-dddddddddddd', 'SBOSS-SEED-001', 'urban', 'template_alpha', 'reach_target', '{"modifiers":["none"]}', 120000, 90000, 1)
ON CONFLICT (level_seed_id) DO NOTHING;

INSERT INTO inventory_items (inventory_item_id, account_id, item_code, quantity)
VALUES ('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'starter_token', 1)
ON CONFLICT (inventory_item_id) DO NOTHING;

INSERT INTO cosmetic_unlocks (cosmetic_unlock_id, account_id, cosmetic_code)
VALUES ('ffffffff-ffff-ffff-ffff-ffffffffffff', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'base_cap')
ON CONFLICT (cosmetic_unlock_id) DO NOTHING;

INSERT INTO match_results (match_result_id, account_id, season_id, level_seed_id, score, clear_time_ms, combo_max, penalties, validation_status)
VALUES ('12121212-1212-1212-1212-121212121212', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'dddddddd-dddd-dddd-dddd-dddddddddddd', 1000, 110000, 12, 0, 'accepted')
ON CONFLICT (match_result_id) DO NOTHING;
