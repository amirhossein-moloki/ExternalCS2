# گزارش بررسی افست‌ها و پیشنهاد ویژگی‌های جدید برای پروژه CS2GameHelper

با بررسی دقیق محتویات پوشه `offsets/` و فایل‌های `client_dll.json` و `offsets.json`، و همچنین مقایسه آن‌ها با ویژگی‌های فعلی پروژه، لیست زیر از ویژگی‌های جدید که با استفاده از داده‌های موجود قابل پیاده‌سازی هستند تهیه شده است:

## ۱. ویژگی‌های بصری (Visuals / ESP)

- **Glow ESP**: با استفاده از کلاس `CGlowProperty` و فیلدهایی مثل `m_iGlowType` و `m_fGlowColor`، می‌توان افکت درخشش دور بازیکنان ایجاد کرد که از پشت دیوار قابل مشاهده باشد.
- **No-Flash**: با استفاده از فیلد `m_flFlashDuration` در کلاس `C_CSPlayerPawnBase`، می‌توان مدت زمان کور شدن بازیکن را به صفر تغییر داد.
- **World ESP (Dropped Items & Grenades)**: با استفاده از کلاس‌هایی مثل `C_BasePlayerWeapon` و `C_BaseCSGrenadeProjectile`، می‌توان سلاح‌های روی زمین افتاده، نارنجک‌های در حال پرتاب و محل دقیق `Inferno` (آتش مولووتوف) را روی صفحه نمایش داد.
- **Recoil Crosshair (Static/Dynamic)**: با استفاده از `m_aimPunchAngle` می‌توان یک نشانه (Crosshair) دوم ایجاد کرد که محل دقیق برخورد تیر را با توجه به لگد سلاح نشان دهد.

## ۲. ویژگی‌های اطلاعاتی (Information / UI)

- **Money ESP**: با استفاده از کلاس `CCSPlayerController_InGameMoneyServices` و فیلد `m_iAccount`، می‌توان مقدار پول تیم حریف را مشاهده کرد که برای مدیریت اقتصاد تیم در بازی بسیار مفید است.
- **Rank & Wins Display**: فیلدهای `m_iCompetitiveRanking` و `m_iCompetitiveWins` در کلاس `CCSPlayerController` امکان نمایش رنک و تعداد بردهای بازیکنان را در `Spectator List` یا بالای سر آن‌ها فراهم می‌کند.
- **Ping Indicator**: فیلد `m_iPing` در `CCSPlayerController` امکان نمایش پینگ دقیق هر بازیکن را فراهم می‌کند.
- **Defuse Kit Indicator**: با استفاده از `m_bPawnHasDefuser` در `CCSPlayerController` می‌توان تشخیص داد کدام یک از بازیکنان تیم CT کیت خنثی‌سازی بمب دارند.

## ۳. ویژگی‌های کمکی و گیم‌پلی (Misc / Gameplay)

- **BunnyHop (Bhop)**: با استفاده از `m_fFlags` (برای تشخیص روی زمین بودن) در کلاس `C_BaseEntity` و شبیه‌سازی کلید `Space`، می‌توان قابلیت پرش خودکار را اضافه کرد.
- **Auto-Strafing**: با ترکیب سرعت (`m_vecAbsVelocity`) و جهت نگاه، می‌توان به بازیکن در انجام پرش‌های بلندتر کمک کرد.
- **Hit Indicator / Damage Logger**: با استفاده از کلاس `CCSPlayerController_DamageServices` و لیست `m_DamageList`، می‌توان مقدار دقیق دمیج وارد شده به هر دشمن را در هر لحظه ثبت و نمایش داد.

## ۴. لیست آفست‌های کلیدی شناسایی شده برای پیاده‌سازی:

| ویژگی | کلاس مربوطه | آفست / فیلد |
| :--- | :--- | :--- |
| **No-Flash** | `C_CSPlayerPawnBase` | `m_flFlashDuration` |
| **Money ESP** | `CCSPlayerController_InGameMoneyServices` | `m_iAccount` |
| **Rank** | `CCSPlayerController` | `m_iCompetitiveRanking` |
| **Ping** | `CCSPlayerController` | `m_iPing` |
| **Velocity** | `C_BaseEntity` | `m_vecAbsVelocity` |
| **Glow** | `C_CSPlayerPawn` | `m_pGlowServices` |
| **Armor** | `C_CSPlayerPawn` | `m_ArmorValue` |

---
**نتیجه‌گیری:**
پروژه در حال حاضر زیرساخت بسیار خوبی برای خواندن آفست‌ها دارد. اکثر داده‌های مورد نیاز برای تبدیل شدن به یک ابزار کامل (External Cheat) در فایل‌های JSON موجود در پوشه `offsets` وجود دارند و فقط نیاز به اضافه کردن منطق مربوطه در بخش `Features/` و تعریف آفست‌های جدید در `Utils/Offsets.cs` دارند.
