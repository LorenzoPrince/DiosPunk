# DIOS PUNK — TECHNICAL LOG

> Bitácora técnica viva. **Se actualiza en cada cambio o implementación.**
> Los commits los realiza el equipo manualmente (GitHub Desktop); Claude solo genera código/archivos y actualiza este log.
> Última actualización: 2026-11-04

---

## 1. Resumen del proyecto

| Campo | Valor |
|---|---|
| Título | Dios Punk |
| Género | Rhythm game + aventura narrativa de texto y diálogos |
| Estética | Punk DIY / fanzine: collage crudo, recortes, B/N, fotocopia de alto contraste, formas angulosas |
| Temática | Homenaje a Javier Messina ("Dios Punk"), Rosario, 2000–2010s. Humor y respeto; no se narra su final |
| Público | 16+ |
| Plataforma | PC Standalone (Windows). Mobile = extra si el scope se amplía |
| Resolución / target | 1920x1080 (16:9), ≥ 60 Hz (ideal 120 Hz) |
| Motor | Unity **6000.5.9f1** |
| VCS | Git + GitHub, Git LFS (audio y texturas) |

### 1.1 Scope
- **Demo:** menú funcional + 1 canción jugable (nivel 1).
- **MVP:** juego de ritmo con ≥ 3 canciones, visuales originales, dificultad creciente, escena narrativa previa a cada canción.
- **Ampliación (en orden):** 5 niveles → respuestas narrativas con consecuencias → estética mixed media.
- **Opcional:** opción de accesibilidad para reducir intensidad visual de debuffs; calibración de latencia + tutorial en primera partida.

### 1.2 Core loop
Nota desciende hacia barra de impacto inferior → input → juicio:
- **Acierto:** suma puntos y combo; racha alta de aciertos carga Modo Punk (LENTO).
- **Fallo:** corta combo, baja vida; racha de fallos carga Modo Punk (RÁPIDO).
- **Debuffs:** cada 25 s durante 5 s (valores de partida, a ajustar en testeo): distorsión visual (shader/ruido) + alteración del chart (notas "injustas", patrones más densos). No se esquivan; se absorben con Modo Punk o se sobreviven.
- **Modo Punk:** al llenarse y activarse → escudo contra 1 Debuff (se consume), ventana de tolerancia más permisiva, distorsión audiovisual estética (saturación fanzine). Vida = 0 ⇒ Game Over aun con Modo Punk.
- **Fin:** canción terminada con vida > 0 = Victoria (desbloquea siguiente nivel); vida = 0 = Game Over.

### 1.3 Flujo por nivel
Menú principal → (primera vez: calibración de latencia + tutorial) → Escena narrativa → Preparación de canción → Cuenta regresiva → Canción → Resultados (Puntaje, Precisión, Combo máx., Usos de Modo Punk) → Guardado + desbloqueo → reintentar / selector / continuar. 5 niveles + epílogo + créditos.

---

## 2. Stack técnico (decisiones cerradas)

| Aspecto | Decisión |
|---|---|
| Render | URP 17.6.0, **2D Renderer** (`Renderer2D.asset`) |
| Input | **Solo** New Input System (`com.unity.inputsystem` 1.20.0, `activeInputHandler: 1`). 4 carriles, teclado (D-F-J-K o flechas) |
| Charts | ScriptableObjects: `SongData` → colección de `NoteData` con timestamps absolutos (segundos / base DSP). Charts de prueba armados en el Inspector |
| Audio | `AudioSource` nativo; sincronía estricta con `AudioSettings.dspTime`. BPM fijo en MVP |
| Narrativa | Sin Ink/Yarn. ScriptableObjects livianos para diálogos/párrafos cortos + respuestas simples |
| UI | uGUI + TextMeshPro |
| Tests | Unity Test Framework 1.7.0 (EditMode para lógica pura) |
| Paquetes a evaluar quitar | Visual Scripting, Multiplayer Center, módulos no usados |

---

## 3. Decisiones de arquitectura (ADR)

Una entrada por decisión; nunca se borran (si cambian: `Superseded by ADR-XXX`).

### ADR-001 — Timing basado en dspTime
- **Estado:** Aceptada · **Fecha:** 2026-11-04
- **Decisión:** el reloj de la canción es `AudioSettings.dspTime` (con `AudioSource.PlayScheduled`). Posición de nota = f(tiempo de canción − timestamp). Prohibido acumular `deltaTime`.
- **Consecuencias:** se necesita `SongClock` único, offset de calibración de latencia por jugador, y timestamp de input convertido a tiempo DSP.

### ADR-002 — Charts como ScriptableObjects
- **Estado:** Aceptada · **Fecha:** 2026-11-04
- **Decisión:** `SongData` (audio, BPM fijo, offset, `NoteData[]`) y `NoteData` (struct serializable: tiempo, carril). Datos inmutables en runtime; el estado vive en estructuras runtime separadas.
- **Consecuencias:** edición en Inspector; si crecen los charts, valorar herramienta de editor propia.

### ADR-003 — Debuffs alteran chart y presentación
- **Estado:** Aceptada · **Fecha:** 2026-11-04
- **Decisión:** `DebuffScheduler` (cada 25 s / 5 s, configurable) dispara un evento; efectos visuales (shader) y de chart (inyección de patrones densos definidos en `SongData`/config) son consumidores desacoplados.
- **Pendiente:** ver Q-A (cómo se definen las notas "injustas").

### ADR-004 — Modo Punk con State Pattern
- **Estado:** Aceptada · **Fecha:** 2026-11-04
- **Decisión:** estados `Charging → Ready/Active → Cooldown`; barra dual alimentada por racha de fallos (carga rápida) y racha de aciertos (carga lenta). Activo: absorbe 1 debuff, ensancha ventanas de juicio, aplica efecto audiovisual.
- **Pendiente:** ver Q-B.

### ADR-005 — Arquitectura orientada a eventos y asmdefs
- **Estado:** Aceptada · **Fecha:** 2026-11-04
- **Decisión:** módulos desacoplados vía eventos (C# events / SO channels), una `.asmdef` por módulo, lógica pura (juicio, combo, vida, Modo Punk) sin dependencias de `MonoBehaviour` para testearla en EditMode.

### ADR-006 — Conversión de timestamps de input a tiempo DSP
- **Estado:** Aceptada · **Fecha:** 2026-11-04
- **Contexto:** `dspTime` avanza a saltos (por buffer de audio); `InputAction.CallbackContext.time` está en la línea de tiempo de `Time.realtimeSinceStartup`.
- **Decisión:** `SongClock` estima `offset = dspTime − realtime` conservando su máximo reciente (el `dspTime` observado solo puede estar retrasado), con decaimiento de 0.5 ms/s y resync si diverge > 100 ms. `SongTime = realtime + offset − songStartDsp`; los inputs se convierten con el mismo offset y se les resta la calibración de latencia.
- **Consecuencias:** `Time.realtimeSinceStartupAsDouble` se usa **solo** como puente de dominios (nunca `Time.time` ni `deltaTime`). Las notas se mueven con `SongTime` (sin calibración); el juicio y los Miss usan `JudgementTime = SongTime − latencia`.

### ADR-007 — Juicio M02: Perfect ±30 / Good ±70 / Miss
- **Estado:** Aceptada · **Fecha:** 2026-11-04 · **Reemplaza** la propuesta de ±40/±80/±120 y el juicio "Great" (Q-C).
- **Decisión:** `HitWindows.Judge(deltaMs)` (lógica pura). Input > 70 ms tarde = no juzga (la nota pasa a Miss al cruzar la ventana). Input temprano entre 70 y 120 ms (`earlyMissMs`) = Miss que consume la nota; más temprano se ignora (sin penalizar taps fantasma).

### ADR-008 — Bot de prueba con timestamps exactos
- **Estado:** Aceptada · **Fecha:** 2026-11-04
- **Decisión:** `RhythmDebugAutoPlayer` planifica un delta por nota y emite el input vía `RhythmInputReader.InjectLanePress` con `SongClock.SongTimeToInputRealtime(...)`, la inversa exacta de la conversión de inputs. Corre con execution order −50 (después de `SongClock`, antes del controlador).
- **Consecuencias:** valida spawn, `HitWindows`, eventos y estadísticas sin error humano; **no** valida el offset `dspTime↔realtime` (usa el mismo) ni la latencia real del dispositivo, que se calibran a mano con el HUD.
- **Pausa:** `SongClock.Pause` congela `SongTime` y detiene el audio; `Resume` reprograma con `PlayScheduled` (+150 ms) y mantiene `SongTime` congelado hasta alcanzar esa posición.

<!-- Añadir ADR-009+ debajo -->

---

## 4. Convenciones de código

- **Namespaces:** `DiosPunk.<Modulo>` (`Core`, `Rhythm`, `Narrative`, `UI`, `Persistence`).
- **Asmdefs:** `DiosPunk.Core` (sin dependencias) ← `Rhythm`, `Narrative`, `UI`, `Persistence`; tests en `DiosPunk.<Modulo>.Tests`.
- **Estilo:** PascalCase tipos/métodos, `_camelCase` campos privados, `[SerializeField] private`, interfaces con prefijo `I`, `[FormerlySerializedAs]` al renombrar.
- **Patrones:** Object Pooling (notas, VFX, feedback de hit), Event-Driven, State Pattern, SOLID.
- **Cero GC en hot paths:** sin LINQ, sin `new` de clases, sin boxing, sin concatenar strings por frame (`TMP.SetText` con formato), colecciones preasignadas, sin `foreach` sobre interfaces.
- **Input:** callbacks del Input System con `InputAction.CallbackContext.time`/`InputEvent` para timestamp, convertido a tiempo DSP; no polling con `Keyboard.current` en juicio.
- **Git/Unity:** nunca separar un asset de su `.meta`; renombrar/mover solo desde el Editor; serialización Force Text; Visible Meta Files; escenas y prefabs pequeños.

---

## 5. Estructura de carpetas (propuesta)

```
Assets/
├── Docs/                 # Este log
├── Scenes/               # Boot, MainMenu, Narrative, SongPrep, Gameplay, Results
├── Settings/             # URP, InputActions, Volume Profiles
├── _Project/
│   ├── Scripts/{Core,Rhythm,Narrative,UI,Persistence}
│   ├── Data/{Songs,Dialogues,Config}
│   ├── Audio/  Art/  Prefabs/  Shaders/
└── TextMesh Pro/
```

---

## 6. Control de versiones

- **Commits:** los hace el equipo manualmente en GitHub Desktop. Claude no hace commits.
- **Git LFS:** reglas en `.gitattributes` para `.wav .ogg .mp3 .png .psd .tga .jpg .jpeg`. Además, `.unity .prefab .asset .meta .mat .anim .controller .inputactions` forzados a texto con EOL LF.
- **Nota de migración:** `Assets/TextMesh Pro/` ya tenía PNG/JPG/PSD commiteados como blobs normales. Con las nuevas reglas Git los verá como modificados al convertirlos a punteros LFS; se commitea una vez. (No se reescribe historial.)
- **Excluidos:** `Library/ Temp/ Logs/ UserSettings/ obj/` (ver `.gitignore`).

---

## 7. Estado de módulos

| Módulo | Responsabilidad | Estado |
|---|---|---|
| Core / Bootstrap | Servicios, eventos, flujo de escenas | ⬜ |
| SongClock / Audio (M01) | Reloj `dspTime`, offset, calibración de latencia, pausa | 🟩 Validado en Play Mode (2026-11-04) |
| Chart / Notas (M01) | `SongData`, `NoteData`, spawner, pooling, 4 lanes | 🟩 Validado en Play Mode (2026-11-04) |
| Input (M02) | Acciones D-F-J-K / flechas, timestamp | 🟩 Validado en Play Mode (2026-11-04) |
| Judgement (M02) | Ventanas Perfect/Good/Miss (puntaje y vida pendientes) | 🟩 Ventanas validadas; puntaje/vida ⬜ |
| Debuffs | Scheduler 25 s/5 s, efecto visual + chart | ⬜ |
| Modo Punk | Barra dual, escudo, ventana ampliada, efecto | ⬜ |
| Narrativa | Diálogos SO, respuestas cortas | ⬜ |
| UI / HUD | Menú, preparación, cuenta regresiva, HUD, resultados | ⬜ |
| Persistencia | Progreso, desbloqueo de niveles, ajustes | ⬜ |
| Calibración / Tutorial | Primera partida | ⬜ |
| Accesibilidad | Reducir intensidad visual de debuffs | ⬜ |

Leyenda: ⬜ No iniciado · 🟨 En curso · 🟩 Completo · 🟥 Bloqueado

---

## 8. Changelog técnico

Formato: `YYYY-MM-DD · [módulo] · descripción · archivos`

- 2026-11-04 · [Docs] · Creación de la bitácora · `Assets/Docs/TECHNICAL_LOG.md`
- 2026-11-04 · [Docs] · Versión definitiva con GDD, stack y ADR-001..005 · `Assets/Docs/TECHNICAL_LOG.md`
- 2026-11-04 · [Docs] · Resueltas Q-A, Q-C, Q-E, Q-G; Q-B, Q-D, Q-F con valores provisionales · `Assets/Docs/TECHNICAL_LOG.md`
- 2026-11-04 · [Rhythm] · Prototipo vertical M01+M02 en `Assets/_Project/Scripts/Rhythm/`: `DiosPunk.Rhythm.asmdef`, `NoteData`, `HitWindows`/`HitType`, `SongData`, `SongClock`, `RhythmInputReader`, `NoteView`, `RhythmTrackController`, `RhythmDebugHUD`. Sin compilar/probar en Unity al momento de escribir.
- 2026-11-04 · [Rhythm/Editor] · Herramienta `DiosPunk/Rhythm/Build Test Setup` (`Assets/_Project/Scripts/Rhythm/Editor/`, asmdef `DiosPunk.Rhythm.Editor`): genera sprites placeholder (`Art/Placeholders`), WAV de metrónomo 120 BPM (`Audio/Placeholders`, PCM mono), `Prefabs/Note.prefab`, `Data/Songs/TestSong_120BPM.asset` (64 notas, 1 por beat) y arma la escena (RhythmTrack, HitBar y=-3.5, cámara ortográfica 5, DebugCanvas + RhythmDebugHUD). Idempotente. Scripts M01/M02 compilan sin errores; en Play Mode inició sin errores, el feel de timing aún no se probó con teclado.
- 2026-11-04 · [Rhythm/Debug] · Sistema de Debug & Testing: `RhythmHitFeedback` (pads por carril Verde/Amarillo/Rojo), gizmos de ventanas Perfect/Good/Early Miss en `RhythmTrackController`, `RhythmDebugAutoPlayer` (bot: AlwaysPerfect/AlwaysGood/SimulateMisses/HumanRandom gaussiano con semilla), `RhythmDebugTools` (F1 autoplay, F2 reiniciar, F3 pausa, F4 modo bot, +/- latencia ±5 ms; resumen en consola al terminar/pausar; monitor de GC con ProfilerRecorder), `RhythmStats` (media y desvío estándar por Welford). `SongClock` gana `Pause/Resume` y `SongTimeToInputRealtime` (inversa exacta). El builder del escenario agrega todo. Compila sin errores; la prueba end-to-end en Play Mode no se pudo ejecutar con el editor sin foco.
- 2026-11-04 · [Rhythm] · Validación manual en Play Mode por el equipo: M01/M02 + sistema de debug funcionan según lo esperado (reportado por el usuario; sin cifras de GC/latencia registradas aún).
- 2026-11-04 · [Git] · Reglas LFS (audio/texturas) y normalización de YAML de Unity · `.gitattributes`

---

## 9. Preguntas abiertas / riesgos

| # | Pregunta o riesgo | Estado |
|---|---|---|
| Q-A | Notas "injustas" del debuff | **Resuelta:** patrones densos definidos a mano por canción en `SongData` |
| Q-B | Modo Punk: umbrales, tasas de carga, duración, ampliación de ventana | Abierta → se implementa con **valores provisionales** en un SO de config (ajustables en testeo) |
| Q-C | Juicio | **Resuelta (actualizada, ver ADR-007):** Perfect ±30 / Good ±70 / Miss. Puntaje/precisión: pendiente |
| Q-D | Vida: máximo, daño por fallo, recuperación | Abierta → provisional en SO de config (100 HP, −10 por Miss, +1 por acierto) |
| Q-E | Notas hold | **Resuelta:** solo notas simples en MVP; hold queda para ampliación |
| Q-F | Guardado | Abierta → provisional: JSON en `persistentDataPath` (nivel desbloqueado, mejores resultados, ajustes) |
| Q-G | Teclas | **Resuelta:** D-F-J-K y flechas activos a la vez; sin rebindeo en MVP |
| R-1 | Latencia de audio variable en PC → calibración obligatoria | Riesgo |
| R-2 | Debuffs de shader en 120 Hz: presupuesto de GPU del pase fullscreen en URP 2D | Riesgo |
