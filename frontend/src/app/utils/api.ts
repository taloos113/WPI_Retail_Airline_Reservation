export const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5000/api/v1';

export type Airport = { code: string; name: string; displayName: string; timeZoneId: string };
export type FlightLeg = {
  flightKey: string;
  airline: string;
  flightNumber: string;
  departAirport: string;
  departCode: string;
  arriveAirport: string;
  arriveCode: string;
  departLocalDateTime: string;
  arriveLocalDateTime: string;
  departTimeZone: string;
  arriveTimeZone: string;
};
export type Itinerary = { itineraryId: string; legs: FlightLeg[]; stops: number; totalTravelMinutes: number; totalLayoverMinutes: number };
export type SearchResponse = { airports: Airport[]; outboundItineraries: Itinerary[]; returnItineraries: Itinerary[] };
export type Seat = { seatNumber: string; isAvailable: boolean };
export type SelectedSeat = { flightKey: string; seatNumber: string };
export type ReservationResponse = { confirmationCode: string; tripType: string; reservedSeats: number; outboundLegs: FlightLeg[]; returnLegs: FlightLeg[] };

export type SearchParams = {
  departureAirport: string;
  arrivalAirport: string;
  departureDate: string;
  departureWindowStart?: string;
  departureWindowEnd?: string;
  returnDate?: string;
  returnWindowStart?: string;
  returnWindowEnd?: string;
  sortBy?: string;
};

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, { headers: { 'Content-Type': 'application/json' }, ...options });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Request failed: ${res.status}`);
  }
  return res.json();
}

export function getAirports() {
  return request<Airport[]>('/Flight/airports');
}

export function searchFlights(params: SearchParams) {
  const qs = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value) qs.set(key[0].toUpperCase() + key.slice(1), value);
  });
  return request<SearchResponse>(`/Flight/search?${qs.toString()}`);
}

export function getSeats(flightKey: string) {
  return request<{ flightKey: string; seats: Seat[] }>(`/Flight/seats/${encodeURIComponent(flightKey)}`);
}

export function createReservation(outboundLegs: FlightLeg[], returnLegs: FlightLeg[], selectedSeats: SelectedSeat[]) {
  return request<ReservationResponse>('/Reservation', {
    method: 'POST',
    body: JSON.stringify({ outboundLegs, returnLegs, selectedSeats }),
  });
}
