# راهنمای استفاده از اف‌ست‌های سفارشی (Custom Offsets)

برای استفاده از اف‌ست‌های خودتان در این پروژه، دو روش اصلی وجود دارد. تمام منطق مربوط به بارگذاری اف‌ست‌ها در فایل `Utils/Offsets.cs` مدیریت می‌شود.

## روش ۱: استفاده از فایل‌های محلی (Local) - پیشنهادی
این روش برای زمانی است که می‌خواهید فایل‌های JSON اف‌ست را به صورت دستی در کنار برنامه قرار دهید.

### مراحل:
۱. **آماده‌سازی فایل‌ها**: دو فایل با نام‌های `offsets.json` و `client_dll.json` تهیه کنید. این فایل‌ها باید با فرمت استاندارد (مانند خروجی [CS2-OFFSETS](https://github.com/a2x/cs2-dumper)) باشند.
۲. **قرار دادن فایل‌ها**: یک پوشه به نام `offsets` در کنار فایل اجرایی برنامه (`.exe`) ایجاد کنید و فایل‌های خود را درون آن قرار دهید.
   - مسیر در حالت توسعه: `bin/Debug/net8.0-windows/offsets/`
۳. **اصلاح کد**: برای اینکه برنامه همیشه از فایل‌های محلی استفاده کند و سعی در دانلود نداشته باشد، فایل `Utils/Offsets.cs` را باز کرده و متد `UpdateOffsets` را به شکل زیر تغییر دهید:

```csharp
public static async Task UpdateOffsets()
{
    string? offsetsJson = null;
    string? clientJson = null;

    try
    {
        // مستقیم فایل‌های محلی را بخوان
        if (!File.Exists(_localOffsetsPath) || !File.Exists(_localClientPath))
            throw new FileNotFoundException("Local offset files not found.");

        offsetsJson = await File.ReadAllTextAsync(_localOffsetsPath);
        clientJson = await File.ReadAllTextAsync(_localClientPath);
        Console.WriteLine("[Offsets] Loaded from local cache.");
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Failed to load offsets: {ex.Message}");
    }

    // بقیه کد برای پارس کردن (بدون تغییر باقی بماند)
    using var offsetsDoc = JsonDocument.Parse(offsetsJson);
    // ...
}
```

## روش ۲: تغییر آدرس دانلود (Remote)
اگر اف‌ست‌های خود را در یک سرور یا مخزن گیت‌هاب شخصی آپلود کرده‌اید، می‌توانید آدرس‌های پیش‌فرض را تغییر دهید.

### مراحل:
۱. فایل `Utils/Offsets.cs` را باز کنید.
۲. در خطوط ۱۱۹ و ۱۲۰، آدرس‌های URL را با آدرس‌های مستقیم (Direct Link) خود جایگزین کنید:

```csharp
// Utils/Offsets.cs
offsetsJson = await FetchJson("https://your-domain.com/your-offsets.json");
clientJson = await FetchJson("https://your-domain.com/your-client_dll.json");
```

## ساختار مورد انتظار فایل‌های JSON
برنامه انتظار دارد ساختار فایل‌های شما به صورت زیر باشد:

### offsets.json
```json
{
  "client.dll": {
    "dwEntityList": 123456,
    "dwLocalPlayerPawn": 123456
  },
  "engine2.dll": {
    "dwBuildNumber": 123456
  }
}
```

### client_dll.json
```json
{
  "client.dll": {
    "classes": {
      "C_BaseEntity": {
        "fields": {
          "m_iHealth": 123
        }
      }
    }
  }
}
```

**نکته:** اگر نام فیلدها یا ساختار در дамپر شما متفاوت است، باید بخش‌های `destData.m_... = GetField(...)` را در فایل `Offsets.cs` مطابق با آن تغییر دهید.
