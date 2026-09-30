# Project Auga

## Credits

- **Original mod:** [Project Auga](https://github.com/RandyKnapp/Auga) by **RandyKnapp** (code). Upstream credits: "by RandyKnapp / n4"; Nexus Mods ([mod 1413](https://www.nexusmods.com/valheim/mods/1413)): "Code: RandyKnapp, Design: n4".
- **UI art and design:** **n4**.
- **Upstream co-maintenance:** **Vapok** ([Vapok/Auga](https://github.com/Vapok/Auga)).
- **Unity 6 community port:** **mrcook1e-ai** ([mrcook1e-ai/Auga](https://github.com/mrcook1e-ai/Auga)).
- **AugaSkin fork maintenance:** **Morgott** ([UberMorgott](https://github.com/UberMorgott)).

> **Unofficial fork.** AugaSkin is not affiliated with or endorsed by RandyKnapp or the Project Auga team.
> Upstream Auga code and assets remain the property of their authors (all rights reserved); this fork claims no license over them.
> Used with the author's written permission (2026-09-30); see [LICENSING.md](LICENSING.md).

---

Форк [Auga](https://github.com/RandyKnapp/Auga) от **RandyKnapp**, поддерживает **Morgott** ([UberMorgott](https://github.com/UberMorgott)).

Auga — полный UI-оверхол для Valheim. Переработан каждый элемент интерфейса: инвентарь, HUD, крафтинг, экран персонажа, загрузочные экраны и многое другое.

> Этот форк поддерживает актуальную версию Valheim и имеет обновлённую систему сборки, которая **автоматически находит установленную игру** без настройки путей вручную.

---

## Структура проекта

```text
Auga/
├── Auga/                  — основной BepInEx-плагин (C#)
│   ├── Auga.cs            — точка входа, BepInEx Plugin
│   ├── *_Setup.cs         — патчи UI по элементам
│   ├── Compat/            — совместимость с другими модами
│   └── manifest.json      — метаданные мода (Thunderstore)
│
├── AugaUnityLib/          — Unity-компоненты (C#, собирается в Unity.Auga.dll)
│   └── *.cs               — MonoBehaviour: полоски здоровья, вкладки, тултипы...
│
├── AugaUnity/             — Unity-проект (UI prefabs, AssetBundle)
│   └── Assets/            — исходники Unity-сцены и ассетов
│
├── Libs/                  — локальные зависимости
│   ├── fastJSON.dll
│   └── APIManager.dll     — (нужно добавить вручную, см. ниже)
│
├── build/
│   └── FindValheim.ps1    — скрипт автопоиска Valheim через Steam
│
├── Valheim.props          — центральный конфиг путей (пути к игре / BepInEx)
├── Auga.sln               — Visual Studio solution
└── packages/              — NuGet-пакеты (ILRepack, AssemblyPublicizer)
```

---

## Сборка проекта

### Требования

- Visual Studio 2019+ или Rider
- .NET Framework 4.7.2 SDK
- Valheim + [BepInExPack_Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/) — установленные и запущенные хотя бы раз

### Быстрый старт

```bash
git clone https://github.com/<your-repo>/Auga.git
cd Auga
```

1. Открой `Auga.sln` в Visual Studio
2. **Restore NuGet Packages** (правая кнопка на Solution → Restore NuGet Packages)
3. Нажми **Build Solution**

**Valheim находится автоматически.** При сборке запускается [`build/FindValheim.ps1`](build/FindValheim.ps1), который:

- читает реестр Windows → находит путь к Steam
- парсит `steamapps/libraryfolders.vdf` → перебирает все Steam-библиотеки
- возвращает путь к Valheim куда бы он ни был установлен

После сборки `Auga.dll` автоматически копируется в `<Valheim>\BepInEx\plugins\Auga\`.

### Если Valheim не найден автоматически

Передай путь явно при сборке:

```shell
msbuild /p:ValheimDir="D:\Games\Valheim"
```

или отредактируй первую строку в [`Valheim.props`](Valheim.props).

### APIManager.dll

Этот файл не входит в репозиторий. Положи его в `Libs/APIManager.dll` перед сборкой.  
Источник: [Valheim-APIManager](https://github.com/Vapok/Valheim-APIManager)

---

## Как работает сборка

| Инструмент | Назначение |
| --- | --- |
| `BepInEx.AssemblyPublicizer.MSBuild` | Автоматически делает internal-члены Valheim DLL публичными для компиляции |
| `ILRepack` | Упаковывает зависимости (fastJSON, APIManager) в один итоговый DLL |
| `Valheim.props` | Единый файл с путями — все `.csproj` импортируют его |
| `build/FindValheim.ps1` | PowerShell-скрипт автопоиска игры через реестр Steam |

Valheim assemblies, помеченные `<Publicize>true</Publicize>` в `.csproj`, publicize-уются **автоматически при каждой сборке** — никаких ручных шагов.

---

## Конфигурации сборки

| Конфигурация | Описание |
| --- | --- |
| `Debug` | Отладочная сборка, копируется в плагины Valheim |
| `Release` | Оптимизированная сборка |

---

## Установка мода (для игроков)

1. Установи [BepInExPack_Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)
2. Установи с помощью менеджера модов (r2modman / Thunderstore MM)
3. **ИЛИ** вручную: скопируй содержимое папки `files` из архива в  
   `<Valheim>\BepInEx\plugins\Auga\`

---

## Совместимость с другими модами

| Мод | Совместимость |
| --- | --- |
| EpicLoot | ✓ |
| Equipment & Quick Slots | ✓ |

Auga кардинально меняет UI Valheim — скорее всего несовместим с другими UI-модами.

---

## Скриншоты

[Смотреть скриншоты](Auga/Screenshots/)

## Contributor licensing

- Original contributions by Morgott are offered under CC BY-NC 4.0; see [LICENSE](LICENSE) and [full license text](CC-BY-NC-4.0.txt).
- Inherited Auga code and assets, game material and third-party components are excluded; their existing notices and terms remain unchanged. This does not establish permission to relicense upstream material.
