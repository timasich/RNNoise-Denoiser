RNNoise Denoiser (Windows, .NET 8) — Русская версия

Локальная очистка речевого аудио в видео/аудио через FFmpeg‑фильтр arnndn (RNNoise). WinForms‑интерфейс, пакетная обработка, интернет не нужен.


✨ Возможности

Пакетная обработка видео/аудио

Можно не перекодировать видео — чистится только аудио

Настраиваемая цепочка: highpass / lowpass → aresample=48000 → arnndn (RNNoise) → speechnorm

Готово к пресетам (Soft/Standard/Aggressive)

Drag & drop, пути и настройки сохраняются


🔧 Требования

Windows 10/11

.NET 8 Desktop Runtime

FFmpeg с фильтром arnndn

Модель RNNoise (.rnnn)


🔽 Где скачать FFmpeg и модели RNNoise

FFmpeg (Windows-сборки):

Официальная страница: https://ffmpeg.org/download.html

Рекомендованные сборки с arnndn:

Gyan.dev (Full build): https://www.gyan.dev/ffmpeg/builds/

BtbN FFmpeg-Builds: https://github.com/BtbN/FFmpeg-Builds

RNNoise (библиотека и модели):

Проект RNNoise: https://github.com/xiph/rnnoise

Предобученные модели для arnndn:

arnndn-models: https://github.com/richardpl/arnndn-models (начните с std.rnnn)

Сообщество: https://github.com/GregorR/rnnoise-models

Рекомендации по версиям: Используйте FFmpeg 6.x или 7.x (актуальный стабильный). Если опции mix нет — обновите FFmpeg или временно уберите :mix= из фильтра.


🚀 Установка

Установите .NET 8 Desktop Runtime (если не установлен).

Скачайте и распакуйте FFmpeg. Запомните путь к папке bin (например, C:\Tools\ffmpeg\bin).

Скачайте модель RNNoise (например, std.rnnn) и положите по стабильному пути (например, C:\Tools\rnnoise\models\std.rnnn).

Запустите приложение и укажите эти пути в верхней панели.


🖱️ Использование

Укажите пути к ffmpeg/bin и файлу модели .rnnn.

Добавьте файлы (видео/аудио) в очередь (кнопки или drag & drop).

Выберите папку вывода, настройте mix/фильтры → Старт.

По умолчанию видео поток копируется без перекодирования, чистится только аудио.


🔬 Быстрая проверка в консоли (необязательно)

:: Проверить наличие arnndn
ffmpeg -hide_banner -filters | findstr arnndn
ffmpeg -hide_banner -h filter=arnndn

:: Пробный запуск (поправьте пути; на Windows лучше слеши вперёд)
ffmpeg -i "input.wav" -af aresample=48000,arnndn=m=C:/Tools/rnnoise/models/std.rnnn -ar 48000 "out.wav"


⚙️ Рекомендуемые пресеты

Soft: mix=0.90

Standard: mix=0.85 (по умолчанию)

Aggressive: mix=0.70 + highpass=80 + lowpass=12000

Подсказка: если звук «подводный», поднимите mix или добавьте мягкий lowpass (11–13 кГц). Держите aresample=48000 перед arnndn.


🧩 Как это работает (кратко)

arnndn загружает компактную RNN‑модель (.rnnn) для подавления шумов вне речи.

Перед RNNoise ресемплим до 48 кГц, после — при желании нормализуем речь (speechnorm).

Для видео можно копировать видеопоток; перекодируется только аудио.


🧰 Troubleshooting

FFmpeg code -22 (Invalid argument)

Чаще всего неверный путь к модели в arnndn.

На Windows не используйте одинарные кавычки; пишите так: m=C:/path/to/model.rnnn.

Если вся строка -af взята в кавычки, уберите их при возможности.

Option 'mix' not found'

Старая сборка FFmpeg или без этой опции. Обновите FFmpeg или временно уберите :mix=.

No such filter: 'arnndn'

Сборка FFmpeg без фильтра. Поставьте сборку, где arnndn есть (Gyan Full / BtbN).

Error opening model / No such file or directory

Проверьте путь к модели. Используйте прямые слеши и убедитесь, что файл доступен.

Артефакты / «подводный» звук

Увеличьте mix (0.85 → 0.9). Добавьте lowpass=11000–13000. Попробуйте другую .rnnn‑модель.

Расхождение аудио/видео

Используется -shortest и копирование видео. Если рассинхрон остаётся — попробуйте ремукс/пере‑код аудио с постоянным битрейтом.


📦 Сборка из исходников (по желанию)

Visual Studio 2022 или dotnet build -c Release

Публикация: dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false


🔐 Конфиденциальность

Всё работает локально. Аудио никуда не отправляется.


☕ Поддержать разработчика
Если инструмент экономит вам время — можно поблагодарить разработчика:

- Ko-fi: https://ko-fi.com/timasich
- Donation Alerts: https://www.donationalerts.com/r/timasich

Спасибо! ❤️


📖 Условия использования (кратко)
- Приложение работает **локально** и обрабатывает файлы на вашем компьютере.
- Используются **FFmpeg** и внешние **RNNoise-модели**. Вы сами соблюдаете их лицензии.
- Поставляется **«как есть»**, без гарантий и ответственности. Используйте на свой риск.
- Проект **не распространяет** FFmpeg и модели RNNoise; пути указываете вы.


📜 Лицензия

MIT. Проект не распространяет FFmpeg и модели RNNoise; пути задаёт пользователь.
