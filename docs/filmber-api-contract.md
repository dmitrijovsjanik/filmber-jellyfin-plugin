# Подтверждённый контракт Filmber API

Проверено по текущему коду Filmber 30 июля 2026:

- `src/app/api/external/pair-init/route.ts`
- `src/app/api/external/pair-poll/route.ts`
- `src/app/api/pair-requests/[id]/approve/route.ts`
- `src/app/api/external/sync/route.ts`
- `src/lib/auth/middleware.ts`
- `src/lib/auth/session.ts`
- `src/lib/services/listSyncService.ts`
- `src/lib/services/playbackSyncService.ts`
- `src/lib/services/episodeSyncService.ts`
- `src/lib/services/externalSyncIdempotency.ts`
- `src/lib/db/schema.ts`
- связанные Jest-тесты external API и сервисов

Контракт ниже реализован плагином и минимальными обратно совместимыми
изменениями Filmber. Production migration и deployment не выполнялись.

## Pairing и external session

### `POST /api/external/pair-init`

Без авторизации. Тело:

```json
{
  "deviceInfo": "Jellyfin — Alice",
  "clientType": "jellyfin",
  "externalUserId": "32-character-jellyfin-user-id"
}
```

Ответ:

```json
{
  "pairingId": "uuid",
  "code": "6-symbol-code",
  "expiresAt": "ISO-8601"
}
```

`deviceInfo` обрезается до 200 символов. Endpoint ограничен пятью запросами в
минуту на IP. Для `clientType = jellyfin` обязателен Jellyfin user ID в формате
32 hex-символов. Старый Lampa body без новых полей остаётся допустимым.

### `GET /api/external/pair-poll?id=<pairingId>`

Без авторизации. Возможные состояния: `pending`, `expired`, `rejected`,
`approved`. При `approved` токен отдаётся один раз вместе с Filmber user и
external session. Session дополнительно возвращает подтверждённые
`clientType` и `externalUserId`; плагин сохраняет mapping только при их точном
совпадении с выбранным Jellyfin user.

Токен является Filmber JWT для строки `user_sessions.kind = external`. Это не
Jellyfin API key и не пароль. Все защищённые external endpoints принимают только
`Authorization: Bearer <token>` и отклоняют web session.

## Односторонние операции Jellyfin → Filmber

Подтверждённый общий endpoint:

### `POST /api/external/sync`

```json
{
  "ops": [
    {
      "op": "playback",
      "clientOpId": "stable-id-8-to-128-chars",
      "ts": 1785412800000,
      "tmdbId": 603,
      "mediaType": "movie",
      "percent": 42,
      "positionSeconds": 2520,
      "durationSeconds": 6000,
      "lastPlayedAt": "2026-07-30T12:00:00.000Z"
    }
  ]
}
```

- максимум 100 операций;
- лимит 30 batch-запросов в минуту на external session;
- `clientOpId` дедуплицируется в пределах session;
- повтор с тем же ID и тем же содержимым возвращает сохранённый результат;
- тот же ID с другим содержимым возвращает `conflict`;
- результат каждой операции независим:
  `{ "ts": 1, "clientOpId": "...", "ok": true }`.

Подходящие операции:

| Jellyfin evidence | Filmber op |
|---|---|
| movie start/progress | `playback` с `mediaType: movie` |
| episode start/progress | `playback` с `mediaType: tv`, `tmdbId` родительского сериала, season/episode |
| confirmed movie completion | отдельный `watched` |
| confirmed episode completion | отдельный `episode_watched` |

`playback` переводит тайтл в `watching`, но сам по себе не вызывает `watched`
или `episode_watched`. Для завершения нужна отдельная операция. Порог в
Filmber playback normalizer — 85%.

`episode_watched` требует:

```json
{
  "op": "episode_watched",
  "seriesTmdbId": 1399,
  "seasonNumber": 1,
  "episodeNumber": 1,
  "ts": 1785412800000,
  "clientOpId": "stable-id"
}
```

Повтор не создаёт второй эпизод. Более сильный status `watched` не понижается
в `watching`.

## Jellyfin-специфичная семантика

- external session хранит `clientType = jellyfin` и выбранный
  `externalUserId`;
- list rows, созданные этой session, получают `source = jellyfin`;
- Lampa session и старые pairing requests продолжают использовать прежние
  значения по умолчанию;
- plugin использует только TMDB ID фильма или родительского сериала;
- IMDb fallback пока не отправляется: отдельного подтверждённого Filmber
  endpoint для точного IMDb resolution нет;
- `playback.completed` не заменяет отдельные `watched` и `episode_watched`.

## Доставка

- один Jellyfin event получает стабильный `clientEventId`;
- производные операции используют суффиксы `:playback`, `:watched`,
  `:episode`;
- event удаляется из JSON outbox только после успешного batch-result;
- при сетевой ошибке тот же event остаётся первым в очереди и повторяется;
- unresolved events и события пользователей без mapping не отправляются;
- plugin выполняет только исходящие запросы к Filmber.
