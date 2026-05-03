USE flightdata;

DROP TABLE IF EXISTS reservation_flights;
DROP TABLE IF EXISTS reservations;
DROP TABLE IF EXISTS seat_inventory;

CREATE TABLE seat_inventory (
    seat_inventory_id BIGINT NOT NULL AUTO_INCREMENT,
    flight_key VARCHAR(64) NOT NULL,
    seat_number VARCHAR(6) NOT NULL,
    `row_number` INT NOT NULL,
    seat_letter VARCHAR(1) NOT NULL,
    is_available TINYINT(1) NOT NULL DEFAULT 1,
    reserved_confirmation_code VARCHAR(12) NULL,
    PRIMARY KEY (seat_inventory_id),
    UNIQUE KEY uq_flight_seat (flight_key, seat_number)
);

CREATE TABLE reservations (
    reservation_id BIGINT NOT NULL AUTO_INCREMENT,
    confirmation_code VARCHAR(12) NOT NULL,
    trip_type ENUM('one_way','round_trip') NOT NULL,
    created_at DATETIME NOT NULL,
    PRIMARY KEY (reservation_id),
    UNIQUE KEY uq_confirmation_code (confirmation_code)
);

CREATE TABLE reservation_flights (
    reservation_flight_id BIGINT NOT NULL AUTO_INCREMENT,
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
    PRIMARY KEY (reservation_flight_id),
    CONSTRAINT fk_reservation_flights_reservation
        FOREIGN KEY (reservation_id)
        REFERENCES reservations(reservation_id)
        ON DELETE CASCADE
);