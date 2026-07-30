# Jellyfin verification record

## Version basis

- server version supplied for the target installation: `10.11.8`;
- local Docker image pinned to `jellyfin/jellyfin:10.11.8`;
- plugin packages pinned to `10.11.8`;
- target ABI: `10.11.0.0`;
- framework: `net9.0`.

Official references:

- https://github.com/jellyfin/jellyfin/releases/tag/v10.11.10
- https://github.com/jellyfin/jellyfin/releases/tag/v10.11.8
- https://jellyfin.org/docs/general/installation/container/
- https://github.com/jellyfin/jellyfin-plugin-template
- https://github.com/jellyfin/jellyfin-plugin-playbackreporting

Do not infer compatibility with another server version from this record.

## Spike status

Первичный spike проверен 30 июля 2026 на реальном локальном контейнере
`jellyfin/jellyfin:10.11.10`.

Отдельный compatibility build собран против packages `10.11.8` и загружен в
чистый контейнер `jellyfin/jellyfin:10.11.8`. Сервер подтвердил:

- `Jellyfin version: 10.11.8`;
- assembly `Jellyfin.Plugin.FilmberSync, Version=0.1.0.0` загружена;
- plugin `Filmber Sync 0.1.0.0` активирован;
- `PlaybackEventMonitor` запущен;
- startup завершён без ошибок загрузки плагина.

Плагин успешно загрузился как `Filmber Sync 0.1.0.0` и подписался на
server-side `ISessionManager` events:

- `PlaybackStart`;
- `PlaybackProgress`;
- `PlaybackStopped`.

## Фактические payload

В Jellyfin добавлены локальные 20-секундные movie и episode fixtures с NFO.
Воспроизведение выполнено через Jellyfin Web.

Movie:

- Jellyfin item ID получен из реального server event;
- TMDB `603`;
- IMDb `tt0133093`;
- `Start`, `Progress`, один `Completed` на пороге 85% и отдельный `Stop`;
- duration `20`, финальный progress `100%`.

Episode:

- Jellyfin item и parent series IDs получены из реального server event;
- parent series TMDB `1399`;
- season `1`, episode `1`;
- `Start`, `Progress`, `Completed`;
- episode-level TMDB/IMDb не подставляются вместо series TMDB.

Во всех фактических событиях `resolved = true`. Payload не содержит media path,
Jellyfin server URL, media source, endpoint или token.

## Retry и idempotency evidence

При временно недоступном mock 20 movie events остались в JSON outbox. После
восстановления mock worker доставил все 20, не меняя сохранённые
`clientEventId`, и очистил outbox. Unit-тест отдельно проверяет две попытки
отправки с одним и тем же ID.

## Границы подтверждения

- missing-ID ветка проверена unit-тестом: событие получает `resolved = false`,
  title/year не используются для blind match;
- совместимость с другой версией Jellyfin не заявляется;
- production Filmber не вызывался;
- фактическая двусторонняя синхронизация не проверялась и не реализована.

## Stage 2: локальная односторонняя синхронизация

30 июля 2026 на Jellyfin `10.11.8` подтверждено:

- plugin assembly, controller и playback worker загрузились без startup error;
- настройки доступны только Jellyfin administrator;
- pairing связал выбранный Jellyfin user с matching Filmber user;
- session identity `clientType = jellyfin` и `externalUserId` проверяется перед
  сохранением токена;
- movie TMDB `603`: `playback 0%` → `playback 90%` → `watched`;
- episode parent series TMDB `1399`, S01E01:
  `playback 0%` → `playback 90%` → `episode_watched`;
- после успешной доставки JSON outbox пуст.

Проверка выполнена с локальным симулятором подтверждённого Filmber HTTP
контракта. Реальные route/service изменения Filmber отдельно прошли typecheck и
профильные Jest-тесты. Production migration/deployment не выполнялись.
