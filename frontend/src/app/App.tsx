import { useEffect, useMemo, useState } from 'react';
import { Plane, Search, CalendarDays, Clock, ArrowRight, Armchair, CheckCircle2 } from 'lucide-react';
import { Airport, Itinerary, ReservationResponse, Seat, SelectedSeat, createReservation, getAirports, getSeats, searchFlights } from './utils/api';

type Step = 'search' | 'results' | 'seats' | 'confirm';
type SearchForm = {
  tripType: 'one_way' | 'round_trip';
  departureAirport: string;
  arrivalAirport: string;
  departureDate: string;
  departureWindowStart: string;
  departureWindowEnd: string;
  returnDate: string;
  returnWindowStart: string;
  returnWindowEnd: string;
  sortBy: string;
};

const defaultForm: SearchForm = {
  tripType: 'one_way',
  departureAirport: 'ATL',
  arrivalAirport: 'LAX',
  departureDate: '2023-01-02',
  departureWindowStart: '00:00',
  departureWindowEnd: '23:59',
  returnDate: '',
  returnWindowStart: '00:00',
  returnWindowEnd: '23:59',
  sortBy: 'DepartureTime',
};

function fmtLocal(value: string) {
  const [date, time] = value.split(' ');
  return `${date} ${time?.slice(0, 5) ?? ''}`;
}

function minutes(value: number) {
  const h = Math.floor(value / 60);
  const m = value % 60;
  return `${h}h ${m}m`;
}

export default function App() {
  const [step, setStep] = useState<Step>('search');
  const [airports, setAirports] = useState<Airport[]>([]);
  const [form, setForm] = useState<SearchForm>(defaultForm);
  const [outbound, setOutbound] = useState<Itinerary[]>([]);
  const [returns, setReturns] = useState<Itinerary[]>([]);
  const [selectedOutbound, setSelectedOutbound] = useState<Itinerary | null>(null);
  const [selectedReturn, setSelectedReturn] = useState<Itinerary | null>(null);
  const [seatMap, setSeatMap] = useState<Record<string, Seat[]>>({});
  const [selectedSeats, setSelectedSeats] = useState<Record<string, string>>({});
  const [confirmation, setConfirmation] = useState<ReservationResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => { getAirports().then(setAirports).catch(() => setAirports([])); }, []);

  const legsToReserve = useMemo(() => [...(selectedOutbound?.legs ?? []), ...(selectedReturn?.legs ?? [])], [selectedOutbound, selectedReturn]);

  async function submitSearch(e?: React.FormEvent) {
    e?.preventDefault();
    setLoading(true); setError('');
    try {
      const data = await searchFlights({
        departureAirport: form.departureAirport,
        arrivalAirport: form.arrivalAirport,
        departureDate: form.departureDate,
        departureWindowStart: form.departureWindowStart,
        departureWindowEnd: form.departureWindowEnd,
        returnDate: form.tripType === 'round_trip' ? form.returnDate : undefined,
        returnWindowStart: form.tripType === 'round_trip' ? form.returnWindowStart : undefined,
        returnWindowEnd: form.tripType === 'round_trip' ? form.returnWindowEnd : undefined,
        sortBy: form.sortBy,
      });
      setOutbound(data.outboundItineraries);
      setReturns(data.returnItineraries);
      setSelectedOutbound(null); setSelectedReturn(null); setSelectedSeats({}); setSeatMap({});
      setStep('results');
    } catch (err) { setError(err instanceof Error ? err.message : 'Search failed'); }
    finally { setLoading(false); }
  }

  async function goToSeats() {
    if (!selectedOutbound || (form.tripType === 'round_trip' && !selectedReturn)) { setError('Please select outbound and return itineraries as required.'); return; }
    setLoading(true); setError('');
    try {
      const entries = await Promise.all(legsToReserve.map(async leg => [leg.flightKey, (await getSeats(leg.flightKey)).seats] as const));
      setSeatMap(Object.fromEntries(entries));
      setStep('seats');
    } catch (err) { setError(err instanceof Error ? err.message : 'Could not load seats'); }
    finally { setLoading(false); }
  }

  async function submitReservation() {
    const selected: SelectedSeat[] = legsToReserve.map(leg => ({ flightKey: leg.flightKey, seatNumber: selectedSeats[leg.flightKey] })).filter(s => s.seatNumber);
    if (selected.length !== legsToReserve.length) { setError('Select one available seat for every flight leg.'); return; }
    setLoading(true); setError('');
    try {
      const res = await createReservation(selectedOutbound!.legs, selectedReturn?.legs ?? [], selected);
      setConfirmation(res);
      setStep('confirm');
    } catch (err) { setError(err instanceof Error ? err.message : 'Reservation failed'); }
    finally { setLoading(false); }
  }

  return <main className="shell">
    <header className="hero">
      <div><p className="eyebrow">World Plane Inc.</p><h1><Plane size={34}/> Retail Airline Reservation POC</h1><p>Search, sort, select, reserve seats, and confirm a one-way or round-trip itinerary without login, passenger identity, or payment data.</p></div>
     
    </header>

    {error && <div className="error">{error}</div>}
    {step === 'search' && <section className="card">
      <h2><Search/> Search flights</h2>
      <form onSubmit={submitSearch} className="grid">
        <label>Trip type<select value={form.tripType} onChange={e => setForm({ ...form, tripType: e.target.value as SearchForm['tripType'] })}><option value="one_way">One-way</option><option value="round_trip">Round-trip</option></select></label>
        <label>Departure airport<input list="airports" value={form.departureAirport} onChange={e => setForm({ ...form, departureAirport: e.target.value })}/></label>
        <label>Arrival airport<input list="airports" value={form.arrivalAirport} onChange={e => setForm({ ...form, arrivalAirport: e.target.value })}/></label>
        <datalist id="airports">{airports.map(a => <option key={a.code} value={a.code}>{a.displayName}</option>)}</datalist>
        <label><CalendarDays/> Departure date<input type="date" value={form.departureDate} onChange={e => setForm({ ...form, departureDate: e.target.value })}/></label>
        <label><Clock/> Departure window start<input type="time" value={form.departureWindowStart} onChange={e => setForm({ ...form, departureWindowStart: e.target.value })}/></label>
        <label><Clock/> Departure window end<input type="time" value={form.departureWindowEnd} onChange={e => setForm({ ...form, departureWindowEnd: e.target.value })}/></label>
        {form.tripType === 'round_trip' && <>
          <label>Return date<input type="date" value={form.returnDate} onChange={e => setForm({ ...form, returnDate: e.target.value })}/></label>
          <label>Return window start<input type="time" value={form.returnWindowStart} onChange={e => setForm({ ...form, returnWindowStart: e.target.value })}/></label>
          <label>Return window end<input type="time" value={form.returnWindowEnd} onChange={e => setForm({ ...form, returnWindowEnd: e.target.value })}/></label>
        </>}
        <label>Sort results<select value={form.sortBy} onChange={e => setForm({ ...form, sortBy: e.target.value })}><option value="DepartureTime">Departure time</option><option value="ArrivalTime">Arrival time</option><option value="TravelTime">Total travel time</option></select></label>
        <button disabled={loading}>{loading ? 'Searching...' : 'Search itineraries'}</button>
      </form>
      <p className="note">Demo data is dated around late 2022 and early 2023. Try ATL → LAX on 2023-01-02.</p>
    </section>}

    {step === 'results' && <section className="card">
      <div className="between"><h2>Search results</h2><button className="secondary" onClick={() => setStep('search')}>Edit search</button></div>
      <Results title="Outbound itineraries" list={outbound} selected={selectedOutbound?.itineraryId} onSelect={setSelectedOutbound}/>
      {form.tripType === 'round_trip' && <Results title="Return itineraries" list={returns} selected={selectedReturn?.itineraryId} onSelect={setSelectedReturn}/>} 
      <button onClick={goToSeats} disabled={loading || !selectedOutbound || (form.tripType === 'round_trip' && !selectedReturn)}>{loading ? 'Loading seats...' : 'Continue to seat reservation'} <ArrowRight size={16}/></button>
    </section>}

    {step === 'seats' && <section className="card">
      <div className="between"><h2><Armchair/> Reserve seats</h2><button className="secondary" onClick={() => setStep('results')}>Back</button></div>
      <p className="note">One seat is required per flight leg. Seat availability is loaded from the database and reserved in a transaction when you confirm.</p>
      {legsToReserve.map(leg => <div key={leg.flightKey} className="seatBlock"><h3>{leg.flightNumber}: {leg.departCode} → {leg.arriveCode}</h3><div className="seats">{(seatMap[leg.flightKey] ?? []).map(seat => <button key={seat.seatNumber} className={`seat ${selectedSeats[leg.flightKey] === seat.seatNumber ? 'selected' : ''}`} disabled={!seat.isAvailable} onClick={() => setSelectedSeats({ ...selectedSeats, [leg.flightKey]: seat.seatNumber })}>{seat.seatNumber}</button>)}</div></div>)}
      <button disabled={loading} onClick={submitReservation}>{loading ? 'Creating reservation...' : 'Confirm reservation'}</button>
    </section>}

    {step === 'confirm' && confirmation && <section className="card success"><CheckCircle2 size={42}/><h2>Reservation confirmed</h2><p className="code">{confirmation.confirmationCode}</p><p>No personal passenger data or payment information was collected. This POC does not support modifying or deleting reservations after creation.</p><button onClick={() => { setStep('search'); setConfirmation(null); }}>Start another search</button></section>}
  </main>;
}

function Results({ title, list, selected, onSelect }: { title: string; list: Itinerary[]; selected?: string; onSelect: (i: Itinerary) => void }) {
  return <div className="results"><h3>{title}</h3>{list.length === 0 && <p className="note">No valid itineraries found for these criteria.</p>}{list.map(item => <button key={item.itineraryId} className={`itinerary ${selected === item.itineraryId ? 'chosen' : ''}`} onClick={() => onSelect(item)}>
    <div className="between"><strong>{item.legs[0].departCode} → {item.legs[item.legs.length - 1].arriveCode}</strong><span>{item.stops === 0 ? 'Nonstop' : `${item.stops} stop`} · {minutes(item.totalTravelMinutes)}</span></div>
    {item.legs.map(leg => <div className="leg" key={leg.flightKey}><span>{leg.airline} {leg.flightNumber}</span><span>{leg.departCode} {fmtLocal(leg.departLocalDateTime)} <ArrowRight size={13}/> {leg.arriveCode} {fmtLocal(leg.arriveLocalDateTime)}</span><small>Local airport time: {leg.departTimeZone} → {leg.arriveTimeZone}</small></div>)}
    {item.totalLayoverMinutes > 0 && <small>Layover time validated by database connection rule: {minutes(item.totalLayoverMinutes)}</small>}
  </button>)}</div>;
}
