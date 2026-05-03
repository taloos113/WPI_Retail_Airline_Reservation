# Submission Notes

This version was adjusted to match the Version 1 SOW.

## Main improvements

1. Removed passenger personal information and payment flow from the frontend.
2. Added departure and return time-window fields.
3. Added UI sorting by departure time, arrival time, and total travel time.
4. Added database-backed connection rules through the `connection_rules` table.
5. Added database-backed seat availability through the `seat_inventory` table.
6. Added transactional seat reservation and reservation persistence.
7. Preserved one-way and round-trip reservation workflows.
8. Displays flight times as local airport wall-clock times and labels the airport time zone.
9. Keeps reservation modification/deletion out of scope.

## Known proof-of-concept assumptions

- The supplied sample flight data appears to store flight date-times as local wall-clock times. The UI therefore displays those database values directly and labels each leg with the airport time-zone metadata.
- The initial seat inventory is generated lazily for each flight when seat data is first requested.
- Default connection rules use a 45-minute minimum layover and 240-minute maximum layover.
