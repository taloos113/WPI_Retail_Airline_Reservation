CREATE DATABASE IF NOT EXISTS flightdata;
USE flightdata;

-- Run flightdata_deltas.sql and flightdata_southwests.sql first if the deltas/southwests tables do not already exist.

CREATE TABLE IF NOT EXISTS airports (
  airport_code VARCHAR(3) PRIMARY KEY,
  airport_name VARCHAR(255) NOT NULL,
  time_zone_id VARCHAR(80) NOT NULL
);

INSERT INTO airports (airport_code, airport_name, time_zone_id)
SELECT DISTINCT SUBSTRING_INDEX(SUBSTRING_INDEX(DepartAirport, '(', -1), ')', 1), DepartAirport,
  CASE
    WHEN SUBSTRING_INDEX(SUBSTRING_INDEX(DepartAirport, '(', -1), ')', 1) IN ('LAX','SFO','SEA','SAN','SJC','OAK','BUR','SNA','ONT','SMF','LGB') THEN 'America/Los_Angeles'
    WHEN SUBSTRING_INDEX(SUBSTRING_INDEX(DepartAirport, '(', -1), ')', 1) IN ('DEN','SLC','PHX','LAS','TUS','BOI','ABQ') THEN 'America/Denver'
    WHEN SUBSTRING_INDEX(SUBSTRING_INDEX(DepartAirport, '(', -1), ')', 1) IN ('ORD','MDW','MSP','DAL','DFW','HOU','IAH','MCI','STL','BNA','AUS','SAT','MSY') THEN 'America/Chicago'
    ELSE 'America/New_York'
  END
FROM deltas
ON DUPLICATE KEY UPDATE airport_name=VALUES(airport_name), time_zone_id=VALUES(time_zone_id);

INSERT INTO airports (airport_code, airport_name, time_zone_id)
SELECT DISTINCT SUBSTRING_INDEX(SUBSTRING_INDEX(ArriveAirport, '(', -1), ')', 1), ArriveAirport,
  CASE
    WHEN SUBSTRING_INDEX(SUBSTRING_INDEX(ArriveAirport, '(', -1), ')', 1) IN ('LAX','SFO','SEA','SAN','SJC','OAK','BUR','SNA','ONT','SMF','LGB') THEN 'America/Los_Angeles'
    WHEN SUBSTRING_INDEX(SUBSTRING_INDEX(ArriveAirport, '(', -1), ')', 1) IN ('DEN','SLC','PHX','LAS','TUS','BOI','ABQ') THEN 'America/Denver'
    WHEN SUBSTRING_INDEX(SUBSTRING_INDEX(ArriveAirport, '(', -1), ')', 1) IN ('ORD','MDW','MSP','DAL','DFW','HOU','IAH','MCI','STL','BNA','AUS','SAT','MSY') THEN 'America/Chicago'
    ELSE 'America/New_York'
  END
FROM deltas
ON DUPLICATE KEY UPDATE airport_name=VALUES(airport_name), time_zone_id=VALUES(time_zone_id);

INSERT INTO airports (airport_code, airport_name, time_zone_id)
SELECT DISTINCT SUBSTRING_INDEX(SUBSTRING_INDEX(DepartAirport, '(', -1), ')', 1), DepartAirport,
  CASE
    WHEN SUBSTRING_INDEX(SUBSTRING_INDEX(DepartAirport, '(', -1), ')', 1) IN ('LAX','SFO','SEA','SAN','SJC','OAK','BUR','SNA','ONT','SMF','LGB') THEN 'America/Los_Angeles'
    WHEN SUBSTRING_INDEX(SUBSTRING_INDEX(DepartAirport, '(', -1), ')', 1) IN ('DEN','SLC','PHX','LAS','TUS','BOI','ABQ') THEN 'America/Denver'
    WHEN SUBSTRING_INDEX(SUBSTRING_INDEX(DepartAirport, '(', -1), ')', 1) IN ('ORD','MDW','MSP','DAL','DFW','HOU','IAH','MCI','STL','BNA','AUS','SAT','MSY') THEN 'America/Chicago'
    ELSE 'America/New_York'
  END
FROM southwests
ON DUPLICATE KEY UPDATE airport_name=VALUES(airport_name), time_zone_id=VALUES(time_zone_id);

INSERT INTO airports (airport_code, airport_name, time_zone_id)
SELECT DISTINCT SUBSTRING_INDEX(SUBSTRING_INDEX(ArriveAirport, '(', -1), ')', 1), ArriveAirport,
  CASE
    WHEN SUBSTRING_INDEX(SUBSTRING_INDEX(ArriveAirport, '(', -1), ')', 1) IN ('LAX','SFO','SEA','SAN','SJC','OAK','BUR','SNA','ONT','SMF','LGB') THEN 'America/Los_Angeles'
    WHEN SUBSTRING_INDEX(SUBSTRING_INDEX(ArriveAirport, '(', -1), ')', 1) IN ('DEN','SLC','PHX','LAS','TUS','BOI','ABQ') THEN 'America/Denver'
    WHEN SUBSTRING_INDEX(SUBSTRING_INDEX(ArriveAirport, '(', -1), ')', 1) IN ('ORD','MDW','MSP','DAL','DFW','HOU','IAH','MCI','STL','BNA','AUS','SAT','MSY') THEN 'America/Chicago'
    ELSE 'America/New_York'
  END
FROM southwests
ON DUPLICATE KEY UPDATE airport_name=VALUES(airport_name), time_zone_id=VALUES(time_zone_id);

CREATE TABLE IF NOT EXISTS connection_rules (
  rule_id INT AUTO_INCREMENT PRIMARY KEY,
  rule_name VARCHAR(80) NOT NULL,
  min_layover_minutes INT NOT NULL,
  max_layover_minutes INT NOT NULL,
  baggage_transfer_required BOOLEAN NOT NULL DEFAULT TRUE,
  active BOOLEAN NOT NULL DEFAULT TRUE
);

INSERT INTO connection_rules(rule_name, min_layover_minutes, max_layover_minutes, baggage_transfer_required, active)
SELECT 'Default passenger gate transfer and baggage-transfer rule', 45, 240, TRUE, TRUE
WHERE NOT EXISTS (SELECT 1 FROM connection_rules WHERE active=TRUE);

CREATE TABLE IF NOT EXISTS seat_inventory (
  seat_inventory_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  flight_key VARCHAR(64) NOT NULL,
  seat_number VARCHAR(6) NOT NULL,
  row_number INT NOT NULL,
  seat_letter VARCHAR(1) NOT NULL,
  is_available BOOLEAN NOT NULL DEFAULT TRUE,
  reserved_confirmation_code VARCHAR(12) NULL,
  UNIQUE KEY uq_flight_seat (flight_key, seat_number)
);

CREATE TABLE IF NOT EXISTS reservations (
  reservation_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  confirmation_code VARCHAR(12) NOT NULL UNIQUE,
  trip_type ENUM('one_way','round_trip') NOT NULL,
  created_at DATETIME NOT NULL
);

CREATE TABLE IF NOT EXISTS reservation_flights (
  reservation_flight_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  reservation_id BIGINT NOT NULL,
  sequence_number INT NOT NULL,
  direction ENUM('outbound','return') NOT NULL,
  flight_key VARCHAR(64) NOT NULL,
  airline VARCHAR(40) NOT NULL,
  flight_number VARCHAR(40) NOT NULL,
  depart_airport VARCHAR(255) NOT NULL,
  depart_code VARCHAR(3) NOT NULL,
  arrive_airport VARCHAR(255) NOT NULL,
  arrive_code VARCHAR(3) NOT NULL,
  depart_local_datetime DATETIME NOT NULL,
  arrive_local_datetime DATETIME NOT NULL,
  seat_number VARCHAR(6) NOT NULL,
  FOREIGN KEY (reservation_id) REFERENCES reservations(reservation_id)
);
