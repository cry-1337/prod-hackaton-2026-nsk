INSERT INTO users (id, email, nickname, password_hash) VALUES
    ('00000000-0000-4000-8000-000000000001', 'upgrade-organizer@example.test', 'Организатор', :'password_hash'),
    ('00000000-0000-4000-8000-000000000002', 'upgrade-member@example.test', 'Участник', :'password_hash');

INSERT INTO group_purchases
    (id, invite_code, product_id, organizer_id, created_at, deadline, status, regular_price_kopecks, sale_unit, reward_kind)
SELECT gen_random_uuid(), code, '019b0000-0000-7000-8000-000000000001', '00000000-0000-4000-8000-000000000001',
       now() - interval '2 days', now() + remaining, state, 120000, 1, 1
FROM (VALUES
    ('legacy-cancelled', 3, interval '-1 day'),
    ('legacy-expiring', 1, interval '-1 hour'),
    ('legacy-success', 2, interval '-1 day'),
    ('legacy-price-drop', 2, interval '-1 day'),
    ('legacy-receipt', 2, interval '-1 day'),
    ('legacy-gift', 1, interval '1 day'),
    ('legacy-member', 1, interval '1 day'),
    ('legacy-revoked', 1, interval '1 day')
) AS groups(code, state, remaining);

INSERT INTO group_purchase_price_tiers (id, group_purchase_id, required_participants, price_kopecks)
SELECT gen_random_uuid(), id, 3, 99000 FROM group_purchases WHERE invite_code LIKE 'legacy-%';

INSERT INTO group_purchase_participants (id, group_purchase_id, user_id, joined_at, direct_paid_kopecks, limit_revoked)
SELECT gen_random_uuid(), id, '00000000-0000-4000-8000-000000000002', created_at,
       CASE WHEN invite_code NOT IN ('legacy-member', 'legacy-revoked') THEN 99000 END, invite_code = 'legacy-revoked'
FROM group_purchases WHERE invite_code LIKE 'legacy-%';

INSERT INTO group_purchase_participants (id, group_purchase_id, user_id, joined_at, direct_paid_kopecks, limit_revoked)
SELECT gen_random_uuid(), id, organizer_id, created_at, 99000, false
FROM group_purchases WHERE invite_code = 'legacy-gift';

INSERT INTO group_limit_orders (participant_id, paid_kopecks, paid_at, delivery_address)
SELECT p.id, 85000, p.joined_at, 'Сохранённый адрес'
FROM group_purchase_participants p JOIN group_purchases g ON p.group_purchase_id = g.id
WHERE g.invite_code = 'legacy-receipt';

UPDATE group_purchases SET settlement_price_kopecks = 85000 WHERE invite_code = 'legacy-price-drop';
