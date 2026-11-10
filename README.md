# Il mio treno — Mariano Comense ↔ Milano Cadorna

App .NET MAUI (solo Android, uso personale) con partenze in tempo reale, ritardo, affollamento, dettaglio fermate e storico dei treni presi.

## Struttura

| Progetto | Target | Contenuto |
|---|---|---|
| `src/TrenordTratta.Core` | net8.0 | Client API, merge dati, SQLite, impostazioni. Nessuna dipendenza da MAUI |
| `src/TrenordTratta.Mobile` | net10.0-android | UI MAUI (MVVM con CommunityToolkit.Mvvm) |
| `tests/TrenordTratta.Core.Tests` | net8.0 | xUnit sulla logica di merge, date, parsing, storico |

## Avvio

```bash
dotnet workload install maui-android
dotnet test tests/TrenordTratta.Core.Tests
dotnet build src/TrenordTratta.Mobile -t:Run -f net10.0-android
```

Al primo avvio la tratta di default viene risolta per nome con `autocompletaStazione` di ViaggiaTreno e i codici vengono salvati. Se la ricerca non trova una stazione, compare un errore: vai in **Impostazioni → Tratte**, apri la tratta e scegli la stazione dall'elenco.

## Flusso dati

1. `partenze/{codStazione}/{data}` (ViaggiaTreno) dalla stazione d'origine, su più finestre da 60' (da −30' fino al lookahead).
2. Dedup sulla tripla **numero + origine + giorno**.
3. Per i treni Trenord (`codiceCliente == 63`) `train/{numero}` su Trenord: viaggio con `train.date` = oggi, `pass_list` non vuota, preferendo quello che passa dall'origine e ha `actual_data`.
4. Il treno resta in lista solo se la destinazione compare **dopo** l'origine tra le fermate (esclude i treni verso Asso/Canzo). Se le fermate non sono disponibili, vale il capolinea.
5. Fermate senza `station`: nome e orari da `andamentoTreno` di ViaggiaTreno (mappatura posizionale solo se le liste hanno la stessa lunghezza).
6. Ritardo: partenza reale dall'origine → ultima fermata transitata → `ritardo` di ViaggiaTreno.

Resilienza (Polly via `Microsoft.Extensions.Http.Resilience`): timeout totale 30s, 3 retry esponenziali con jitter su errori di rete, 403, 408, 429 e 5xx, timeout per tentativo 8s. Una risposta è un errore se lo status ≠ 200 **o** il body contiene `Error`.

## Funzioni

- **Partenze**: orario, pallino affollamento (se attivo), ritardo verde/rosso con soglia configurabile, binario, arrivo previsto. Pull-to-refresh e auto-refresh (solo con app in primo piano).
- **Direzione automatica**: andata prima dell'ora di cambio (default 13), ritorno dopo; il pulsante ⇅ inverte fino al prossimo cambio di fascia.
- **Dettaglio treno**: fermate con orari programmati e reali, binari, soppressioni, affollamento con fonte.
- **Treni presi**: swipe a destra sulla riga o pulsante nel dettaglio. Ritardo e affollamento dei viaggi di oggi si aggiornano a ogni refresh.
- **Storico**: lista per giorno (più recente in alto), con riepilogo; swipe a sinistra per eliminare.
- **Tratte**: aggiunta/modifica con ricerca stazioni; selettore nella testata quando ce n'è più di una.

## Da verificare sul campo

- **Copertura Ferrovienord su ViaggiaTreno**: Mariano Comense e Milano Cadorna sono su rete FN. Se `partenze` restituisce poco o nulla per queste stazioni, il punto da cambiare è `DepartureService.FetchVtWindowsAsync`.
- **`codiceCliente`**: nel JSON ViaggiaTreno il campo si chiama così; il DTO legge anche `client_code`.
- **`crowding.source`**: i valori possibili non sono documentati. Nel dettaglio treno viene mostrata la fonte: dopo qualche giorno puoi restringere le fonti affidabili nelle impostazioni.
- I campi di `andamentoTreno` usati per il fallback (`arrivoReale`, `partenzaReale`, `actualFermataType`) sono quelli noti dell'API, non documentati ufficialmente.

## Sicurezza

- ViaggiaTreno è solo HTTP: `network_security_config.xml` consente il cleartext **solo** per `www.viaggiatreno.it`; tutto il resto resta HTTPS. Non vengono inviati dati personali.
- Le due API non sono ufficiali e possono cambiare senza preavviso: il parsing è tollerante (numeri come stringhe, booleani come "true"/"1") e i fallimenti per singolo treno non bloccano la lista.
- Nessun segreto nel codice; il DB SQLite resta nella sandbox dell'app.
 