

-- CREATE TABLE pricing_limits (
--     id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
--     instrument_id UUID NOT NULL REFERENCES instruments(id),
--     min_mid NUMERIC(18,8) NOT NULL,
--     max_mid NUMERIC(18,8) NOT NULL,
--     max_spread NUMERIC(18,8) NOT NULL,
--     record_date TIMESTAMP,
--     record_user VARCHAR(100),
--     record_create_date TIMESTAMP
-- );

-- Örnek veri (instruments tablosundaki id'ler ile):
-- INSERT INTO pricing_limits (id, instrument_id, min_mid, max_mid, max_spread, record_user, record_create_date)
-- SELECT gen_random_uuid(), id, 1.0, 2.0, 0.001, 'system', NOW() FROM instruments WHERE symbol = 'EURUSD';
