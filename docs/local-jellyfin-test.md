# Локальная проверка Jellyfin 10.11.8

## Сборка и тесты

```bash
./scripts/dotnet.sh restore FilmberSyncForJellyfin.sln
./scripts/dotnet.sh test FilmberSyncForJellyfin.sln --configuration Release
./scripts/package.sh
```

## Тестовые медиа и сервер

```bash
./scripts/create-test-media.sh
python3 scripts/mock-filmber-endpoint.py
docker compose up -d
```

Открыть `http://localhost:8096`, завершить первоначальную настройку и добавить:

- Movies: `/media/Movies`
- Shows: `/media/Shows`

Плагин уже смонтирован в `/config/plugins/Filmber Sync`. После первого старта
проверить Dashboard → Plugins → Filmber Sync, включить синхронизацию и указать:

```text
http://host.docker.internal:8787
```

Нажать Connect напротив Jellyfin user. Локальный симулятор автоматически
подтвердит код `LOCAL1`; production Filmber он не вызывает.

Перезапустить Jellyfin после установки или обновления DLL.

Проверить по очереди:

1. начать фильм, перемотать, остановить;
2. начать фильм и перейти за 85%;
3. начать эпизод, остановить;
4. начать эпизод и перейти за 85%;
5. открыть `docker/mock-events.jsonl`.

Файл содержит фактические Filmber `ops`. Ожидается:

- movie progress: `playback` с TMDB фильма;
- movie ≥85%: `playback` + `watched`;
- episode progress: `playback` с TMDB родительского сериала, season/episode;
- episode ≥85%: `playback` + `episode_watched`.

Запрос не содержит media path, имя torrent-файла или адрес Jellyfin.

## Ручная установка ZIP

1. Распаковать `artifacts/jellyfin-10.11.8/repository/filmber-sync_0.2.0.0_jellyfin-10.11.8.zip`.
2. Создать папку `Filmber Sync` в каталоге plugins Jellyfin.
3. Скопировать туда `Jellyfin.Plugin.FilmberSync.dll`.
4. Перезапустить Jellyfin.

## Локальный Plugin Repository

```bash
cd artifacts/jellyfin-10.11.8/repository
python3 -m http.server 8765
```

В Dashboard → Plugins → Repositories добавить:

```text
http://host.docker.internal:8765/manifest.json
```

Для Jellyfin вне Docker заменить host на адрес, доступный именно этому серверу.
Manifest и ZIP локальные; они не опубликованы.
