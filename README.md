# Filmber Sync for Jellyfin

Плагин автоматически сохраняет в Filmber:

- прогресс просмотра фильмов и сериалов;
- просмотренные фильмы;
- просмотренные эпизоды.

Синхронизация работает на сервере Jellyfin, поэтому не зависит от клиента:
Jellyfin Web, телевизор, мобильное приложение и другие клиенты отправляют
события через один и тот же сервер.

Каждый пользователь Jellyfin подключается к своему аккаунту Filmber. Пароль,
Jellyfin API key или вручную созданный токен не нужны: плагин показывает
одноразовый код, а Filmber после подтверждения сам выдаёт отдельную сессию.

## Установка и подключение

Пошаговая инструкция для пользователя:
[`docs/installation.md`](docs/installation.md).

Коротко:

1. В Jellyfin открыть «Панель управления → Плагины → Репозитории».
2. Добавить репозиторий:
   `https://raw.githubusercontent.com/dmitrijovsjanik/filmber-jellyfin-plugin/main/repository/manifest.json`.
3. Установить Filmber Sync из каталога и перезапустить Jellyfin.
4. Открыть «Панель управления → Плагины → Filmber Sync».
5. Нажать «Подключить» напротив пользователя Jellyfin.
6. В Filmber открыть «Профиль → Настройки → Jellyfin», ввести код и подтвердить.

Готовые ZIP-сборки также опубликованы в
[GitHub Releases](https://github.com/dmitrijovsjanik/filmber-jellyfin-plugin/releases).

## Совместимость

- проверенный Jellyfin Server: `10.11.8`;
- Jellyfin packages: `10.11.8`;
- target ABI: `10.11.0.0`;
- .NET target: `net9.0`.

Реальная загрузка, pairing и синхронизация фильма и эпизода проверены локально
на Jellyfin `10.11.8`. Совместимость с другими версиями пока не заявляется.

## Разработка

```bash
./scripts/dotnet.sh restore FilmberSyncForJellyfin.sln
./scripts/dotnet.sh build FilmberSyncForJellyfin.sln --configuration Release
./scripts/dotnet.sh test FilmberSyncForJellyfin.sln --configuration Release
./scripts/package.sh
```

Сборка создаёт:

- `artifacts/jellyfin-10.11.8/repository/filmber-sync_0.2.0.0_jellyfin-10.11.8.zip`;
- `artifacts/jellyfin-10.11.8/repository/manifest.json`;
- `artifacts/jellyfin-10.11.8/build.yaml`.

Локальный end-to-end сценарий с тестовым Filmber endpoint описан отдельно в
[`docs/local-jellyfin-test.md`](docs/local-jellyfin-test.md). Подтверждённый
контракт API — в [`docs/filmber-api-contract.md`](docs/filmber-api-contract.md).
