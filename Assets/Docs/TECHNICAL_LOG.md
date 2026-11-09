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

### ADR-009 — Puntaje, multiplicador y vida como lógica pura + sesión + presenter (Hito 2)
- **Estado:** Aceptada · **Fecha:** 2026-11-08
- **Decisión:** `ScoreSystem` y `HealthSystem` son clases C# puras (testeables en EditMode) con eventos `Action`. `RhythmSession` (MonoBehaviour) las alimenta desde `RhythmTrackController.NoteJudged`/`SongStarted`. `RhythmHudPresenter` solo se suscribe a eventos (sin `Update`, sin polling) y escribe en TMP con `SetText` numérico (sin GC).
- **Reglas:** Perfect 100 pts, Good 50 pts, Miss 0 y combo a 0. Puntos × multiplicador vigente **antes** de sumar el combo del propio acierto. Multiplicador x1→x4: +1 cada 10 aciertos seguidos (provisional, configurable). Precisión ponderada = (Perfect×1 + Good×0.5) / juzgadas × 100. Vida 100: Miss −15, Perfect +2, Good +1, tope 100. A 0 HP se emite `HealthDepleted` una vez por pasada (el Game Over aún no está conectado; la pista no se detiene).
- **Consecuencias:** `ScoreSystem` repite el combo que ya lleva `RhythmStats` (este último es solo métrica de debug). Las dos clases comparten el `HitType` del evento de juicio, sin conocer la UI.

### ADR-010 — Estética de pista según bocetos del GDD
- **Estado:** Aceptada · **Fecha:** 2026-11-08 · **Resuelve** Q-I y Q-J.
- **Decisión:** carriles con color: 0 rojo, 1 amarillo, 2 verde, 3 azul (`RhythmTrackController._laneColors`). Notas blancas con borde negro, teñidas por carril; las notas de debuff oscurecen el color del carril (50 %). Receptáculos translúcidos (α 0.45) con la forma de la nota, más una línea de HitBar. Highway blanco con 5 líneas negras. Paneles HUD con 9-slice: Modo Punk arriba izquierda (barra aún sin lógica, Hito 3), puntaje + multiplicador + precisión arriba derecha, vida arriba al centro. Textos de debug movidos a la zona inferior.
- **Notas sostenidas (Q-I):** quedan fuera del MVP; la arquitectura queda abierta (`NoteData` es un struct extensible, el juicio vive en `HitWindows`).

### ADR-011 — Navegación: máquina de estados pura + orquestador + paneles por estado
- **Estado:** Aceptada · **Fecha:** 2026-11-08
- **Decisión:** assembly propio `DiosPunk.Flow` (referencia a `DiosPunk.Rhythm`, nunca al revés). `GameStateMachine` (clase pura) valida transiciones entre `MainMenu, LevelSelect, Options, PreLevel, SongPreparation, Gameplay, PauseMenu, GameOver, ResultsScreen` y recuerda desde dónde se abrió Options. `GameFlowController` es el único que une pantallas, ritmo y progreso. `ScreenManager` solo muestra/oculta `ScreenPanel` (lista de estados visibles por panel) ante cambios de estado. Cada pantalla es un MonoBehaviour fino que expone eventos `Action` de sus botones; no conoce al flujo.
- **Una sola escena** (`SampleScene`) con `HudCanvas`; las pantallas son GameObjects desactivables. El mundo de juego (`RhythmTrack`) y el HUD se activan en Gameplay/Pausa/Game Over (y en Options abierta desde la pausa).
- **Reglas de transición:** Gameplay→Pausa/GameOver/Results; Pausa→Gameplay/Options/Selector/Menú; GameOver→Gameplay (reintentar)/Selector/Menú; Results→PreLevel/Selector/Menú; Options→MainMenu|PauseMenu (según origen).

### ADR-012 — Integración flujo ↔ ritmo
- **Estado:** Aceptada · **Fecha:** 2026-11-08
- **Pausa:** Escape en Gameplay → `RhythmTrackController.PauseSong()` (`SongClock.Pause()`) + PauseMenu; Escape/Reanudar → `ResumeSong()` (`SongClock.Resume()`).
- **Game Over:** `RhythmSession.HealthDepleted` → `StopSong()` + GameOver. Para que detener la canción dentro del juicio sea seguro, `RhythmTrackController.Update` corta si `_running` pasó a false tras `ProcessLateMisses`.
- **Victoria:** `SongFinished` con vida > 0 → arma `ResultsData` desde `ScoreSystem` y `RhythmStats`, guarda progreso y muestra Results. Usos de Modo Punk = 0 hasta el Hito 3.
- **Reintentar:** desde Pausa y Game Over reinicia la canción directo; desde Resultados vuelve a PreLevel. Salir (pausa) → Selector. Selector/Menú detienen la pista (`StopSong`) antes de cambiar de estado.
- **Niveles:** `LevelData` (ScriptableObject: título, escenario, dificultad, `SongData`, diálogo). `RhythmTrackController.LoadSong` cambia la canción; `_playOnStart` queda en false (arranca el flujo).
- **Progreso:** `ProgressStore` en JSON (`persistentDataPath/progress.json`): niveles desbloqueados y mejor puntaje por nivel; I/O solo en eventos.
- **Puntaje en HUD:** `ScoreFormatter` escribe "293.000" en un `char[]` preasignado y `TMP.SetCharArray` (sin GC) — resuelve Q-K.

### ADR-013 — Arquitectura multiescena (MainMenu + Gameplay) y carga asíncrona
- **Estado:** Aceptada · **Fecha:** 2026-11-09 · **Reemplaza** la parte "una sola escena con todas las pantallas" de ADR-011.
- **Escenas** (`Assets/_Project/Scenes/`, ambas en Build Settings):
  - `MainMenu.unity`: menú principal, selector de niveles (1 a 5), opciones y créditos. Orquestador: `MenuFlowController`. No carga audio.
  - `Gameplay.unity` (antes `SampleScene`): pista de ritmo, HUD, narrativa pre-nivel, preparación, pausa, Game Over y resultados. Orquestador: `GameplayFlowController`. Opciones también existe aquí (se abre desde la pausa).
- **Base común:** `FlowControllerBase` expone la `GameStateMachine` a `ScreenManager`. `GameState` suma `Credits`; las transiciones se validan por escena (los cambios entre escenas los hace el `SceneLoader`).
- **`SceneLoader`** (singleton DontDestroyOnLoad creado bajo demanda): `LoadSceneAsync` en modo Single, luego `Resources.UnloadUnusedAssets()` + `GC.Collect()` solo en la transición; expone `IsLoading`, `ProgressChanged` y `Loaded`, y bloquea dobles clics.
- **`GameSession`** (estático): nivel elegido y bandera "abrir selector al volver"; mantiene el `ProgressStore` en memoria. `progress.json` (`persistentDataPath`) persiste entre escenas y ejecuciones; se guarda ante cada victoria. Resetea sus estáticos con `RuntimeInitializeOnLoadMethod` porque el proyecto usa "Enter Play Mode" sin recarga de dominio.
- **Audio bajo demanda:** `LevelData` ya no referencia el `SongData`; guarda su nombre y lo carga con `Resources.Load` desde `Resources/Songs/` solo en Gameplay. El menú nunca carga el audio. Al salir, `GameplayFlowController` suelta su referencia y el `SceneLoader` libera el `AudioClip`. "Continuar" recarga la escena Gameplay con el nivel siguiente para descartar el audio anterior.
- **Datos compartidos:** `LevelCatalog` (ScriptableObject con los 5 `LevelData`) lo referencian ambas escenas.
- **Builder:** `DiosPunk/Scenes/Build MainMenu + Gameplay` regenera ambas escenas y los Build Settings. Recarga los assets tras abrir cada escena (abrir/crear una escena descarga los objetos sin referencias y asignarlos produce referencias nulas).

### ADR-014 — Clics de UI: causa y reglas
- **Estado:** Aceptada · **Fecha:** 2026-11-09
- **Causa del bug:** los canvases generados tenían `Canvas` y `CanvasScaler` pero **no `GraphicRaycaster`**, así que ningún botón recibía clics. El `EventSystem` ya usaba `InputSystemUIInputModule` (sin `StandaloneInputModule`).
- **Reglas:** todo canvas lleva `GraphicRaycaster`; solo los botones, el box del toggle y el slider tienen `Raycast Target` activo (fondos, paneles y textos lo tienen desactivado). El `EventSystem` se crea con `InputSystemUIInputModule` + `AssignDefaultActions()`.

### ADR-015 — Modo Punk (Hito 3)
- **Estado:** Aceptada · **Fecha:** 2026-11-09 · **Resuelve** Q-B.
- **Decisión:** `PunkModeSystem` (clase pura, sin MonoBehaviour) dentro de `RhythmSession`. Carga rápida: cada Miss suma +15 %. Carga lenta: cada racha de 10 aciertos consecutivos suma +5 % (un Miss corta la racha). Al llegar al 100 % se activa solo: otorga 1 escudo, ensancha las ventanas de impacto (Perfect ±30 → ±45 ms, Good ±70 → ±90 ms, `RhythmTrackController._punkWindows`) y la barra se drena en 9 s (`_punkActiveSeconds`, configurable de 1 a 30) hasta apagarse. Mientras está activo no se carga la barra. Si el escudo absorbe un debuff se consume y el modo termina.
- **Tiempo:** el drenaje avanza con `Tick(SongTime)` (dspTime), no con `deltaTime`: pausa y reinicio lo congelan/reinician solos.
- **Desacople:** el sistema solo emite eventos (`BarChanged`, `Activated`, `Deactivated`); `RhythmSession` conecta con el controlador (`SetPermissiveWindows`) y `RhythmHudPresenter` pinta la barra y el rótulo ("MODO PUNK" / "MODO PUNK ACTIVO!") por eventos. `UseCount` alimenta "Usos de Modo Punk" en Resultados.
- **Pendiente:** la distorsión audiovisual del modo activo (saturación fanzine y filtro de audio) no está implementada; el proceso de audio "modo punk" de la guitarra depende del equipo de Audio.

### ADR-016 — Debuffs periódicos (Hito 3)
- **Estado:** Aceptada · **Fecha:** 2026-11-09
- **Decisión:** `DebuffSystem` (clase pura): cada 25 s de canción se dispara un debuff (el primero a los 25 s). Un absorbedor (`PunkModeSystem.TryAbsorbDebuff`) decide: si el Modo Punk está activo, el escudo lo absorbe (evento `Absorbed`, la pista no se penaliza); si no, el debuff dura 5 s (`Started` / `Ended`). Intervalo y duración configurables en `RhythmSession`.
- **Efecto placeholder** (`RhythmDebuffVisuals`): capa de color pulsante sobre la pantalla, temblor de la pista y rótulo "DEBUFF!" / "ESCUDO PUNK: DEBUFF ABSORBIDO". Es solo visual: el juicio usa tiempos, no posiciones.
- **Accesibilidad:** `GameSettings.ReduceVisualEffects` (movido a `DiosPunk.Rhythm`; lo escribe el toggle de Opciones) anula el temblor y deja la capa tenue y fija.
- **Pendiente:** la alteración del chart ("notas injustas" por debuff) requiere patrones autorados por canción (Q-A); aún no se aplica.
- **Pruebas:** F5 fuerza el Modo Punk y F6 dispara un debuff en Play Mode (`RhythmDebugTools`).

### ADR-017 — Controles A-S-D-F y reintento limpio
- **Estado:** Aceptada · **Fecha:** 2026-11-09 · **Reemplaza** el mapeo D-F-J-K de ADR previos.
- **Controles:** carril 0 rojo = A / Flecha Izq; 1 amarillo = S / Flecha Abajo; 2 verde = D / Flecha Arriba; 3 azul = F / Flecha Der (ambos mapeos activos a la vez). Cada receptáculo muestra su indicación ("A / ←", …) en texto de mundo bajo la HitBar.
- **Game Over:** botón OPCIONES entre Reintentar y Selector. `GameState` permite GameOver ↔ Options; `OptionsOpenedOverGameplay` mantiene visible el juego detrás tanto desde la pausa como desde el Game Over, y cerrar Opciones (botón o Escape) vuelve al estado de origen.
- **Reintento:** Pausa y Game Over llaman a `BeginSong`: `LoadSong` no reconstruye la lista de notas si la canción es la misma (sin basura por reintento), `StartSong` libera notas activas, resetea estadísticas y llama a `SongClock.Play` (corta el audio, limpia la pausa, vuelve a muestrear el offset y reprograma desde t = 0). `SongStarted` reinicia puntaje, vida, Modo Punk y debuffs. Al detener la canción (`SongStopped`) se apagan los efectos pero puntaje y vida quedan visibles.
- **Pruebas lógicas:** 22 verificaciones de `PunkModeSystem`, `DebuffSystem`, `HitWindows` y `ScoreFormatter` ejecutadas en el Editor (todas OK). Pendiente: prueba en Play Mode con el editor en foco.

### ADR-018 — NPCs saboteadores y proyectiles (justificación visual del debuff)
- **Estado:** Aceptada · **Fecha:** 2026-11-09
- **Decisión:** `DebuffSystem` suma `Lead` (1.5 s, configurable) y el evento `Incoming`: avisa `Lead` segundos antes del disparo. `NpcSabotageController` (módulo de ritmo) reacciona por eventos: al `Incoming` un NPC lateral (alternando izquierda/derecha) se inclina y arroja un proyectil saturado en arco hacia la base de la pista; el proyectil llega justo cuando el sistema resuelve el debuff. Si el Modo Punk está activo (`Absorbed`) el proyectil choca contra el sprite "Escudo Punk" y se desintegra, sin penalización. Si no (`Started`) impacta, estalla y se despliega el marco inferior dentado (UI) teñido con el color del proyectil; el marco se oculta con `Ended`.
- **Tiempo:** la posición del proyectil se calcula con `SongTime` (dspTime), así que se congela en pausa; los fundidos son cosméticos (`unscaledDeltaTime`). `Update` solo trabaja con un efecto en curso y no genera garbage.
- **Accesibilidad:** con `GameSettings.ReduceVisualEffects` el marco queda tenue y fijo (sin pulso).
- **Placeholders:** siluetas tipo recorte (borde blanco), estrella dentada, anillo de escudo y marco de dientes se generan por código (`RhythmFxBuilder`) en `Art/Placeholders/`. Pendiente: arte final de los NPC (animación por frames).
- **Debug:** F6 dispara un debuff sin aviso (el proyectil aparece ya en el punto de impacto).

### ADR-019 — Sistema narrativo pre-nivel (`DialogueData`)
- **Estado:** Aceptada · **Fecha:** 2026-11-09 · **Reemplaza** el diálogo de 3 líneas por nivel dentro de `LevelData`.
- **Decisión:** `DialogueData` (ScriptableObject): líneas (hablante, texto y retrato opcional estilo fotocopia) y, al final, hasta 3 respuestas cortas (`DialogueChoice`) que modifican una variable de `GameSession` (`ChoiceVariable`: `Difficulty` −1/0/+1 y `ScenarioVariant`). `LevelData` referencia un `DialogueData`. `PreLevelDialoguePresenter` (reemplaza a `PreLevelScreen`) muestra la escena con efecto mecanografiado (`TMP_Text.maxVisibleCharacters`, el texto se asigna una vez por línea; `Update` solo corre mientras se escribe), botones Siguiente/Continuar (el primer clic completa la línea) y Saltar narrativa. Al terminar, `Finished` lleva a la Preparación de canción; está protegido contra doble disparo.
- **Efecto de las decisiones:** la dificultad cambia la velocidad de caída (`GameSession.NoteSpeedMultiplier` = 1 + 0.15 × ajuste, aplicado con `RhythmTrackController.SetSpeedMultiplier` solo en lo visual; el juicio sigue usando tiempos) y la preparación muestra el ajuste y la variante de escenario. Las elecciones se reinician al empezar cada escena narrativa.
- **Contenido:** 5 `DialogueData` placeholder (nivel 1 sin decisión, nivel 3 con variante de escenario, el resto con dificultad). El guion real sigue pendiente (Q-O).

### ADR-020 — Primera partida: calibración de latencia y tutorial de controles
- **Estado:** Aceptada · **Fecha:** 2026-11-09
- **Flujo:** `JUGAR` pasa a `CONTINUAR` si existe `progress.json`. Sin progreso, `JUGAR` y `PARTIDA NUEVA` derivan a `CalibrationScreen` (nuevo estado `Calibration`). Con progreso, `PARTIDA NUEVA` pide confirmación. El progreso solo se reemplaza al terminar u omitir la calibración (`ProgressStore.StartNewGame`); volver atrás no borra nada.
- **Calibración:** metrónomo a 100 BPM con `AudioSource.PlayScheduled` sobre dspTime (dos fuentes alternadas) y pulso visual sincronizado con dspTime. Se toman 10 pulsaciones (cualquier tecla); el timestamp (`ctx.time`) se convierte a DSP con `DspRealtimeMapper` (misma técnica que `SongClock`) y el desfase respecto del beat más cercano se promedia: esa media es el `LatencyOffsetMs` recomendado (se muestra con el desvío; avisa si fue irregular). Verificado por script: 10 pulsaciones simuladas a +25 ms dieron +25.9 ms.
- **Persistencia:** `GameSettings.LatencyOffsetMs` (estático en `DiosPunk.Rhythm`) se guarda en `progress.json` (`calibrated`, `latencyOffsetMs`), se aplica al cargar el progreso y `SongClock` lo toma al iniciar (el ajuste en vivo del debug sigue funcionando).
- **Tutorial:** valida los 4 carriles (A-S-D-F o flechas; cada carril se ilumina con su color); al completarlos se habilita "Comenzar nivel 1". "Omitir calibración" va directo al nivel 1 saltándose también el tutorial.
- **Deuda (resuelta en ADR-021):** `DspRealtimeMapper` duplicaba la conversión de `SongClock`; se eliminó. Opciones ahora ofrece recalibrar.

### ADR-021 — Conversión temporal unificada en `SongClock` y recalibración
- **Estado:** Aceptada · **Fecha:** 2026-11-09 · **Resuelve** la deuda de ADR-020.
- **Decisión:** `DspRealtimeMapper` se eliminó. `SongClock` es la única implementación de la conversión dspTime ↔ tiempo real: nuevos `InputRealtimeToDsp(realtime)`, `SampleNow()` y `ResetTimebase()`, y su `Update` mantiene la estimación del offset también cuando no hay canción (menús, calibración). `CalibrationScreen` recibe un `SongClock` (objeto `CalibrationClock` en la escena MainMenu) y calcula el desfase con `InputRealtimeToDsp`: la calibración y el juicio comparten el mismo código.
- **Recalibrar latencia:** botón en Opciones (solo escena MainMenu) con la latencia actual a la vista. Abre `CalibrationScreen` en modo recalibración: sin tutorial, sin "Omitir", botón "GUARDAR"; al aceptar llama a `ProgressStore.SetCalibration` (no toca niveles ni récords) y vuelve a Opciones. `GameStateMachine` recuerda el origen de la calibración (`CalibrationReturnState`) sin pisar el retorno de Opciones.
- **Verificado:** recalibración por script (10 pulsaciones simuladas 12 ms adelantadas → −10.8 ms), retorno a Opciones y no reinicio del progreso.

### ADR-022 — Los 5 niveles: narrativa, canción y chart enlazados
- **Estado:** Aceptada · **Fecha:** 2026-11-09 · **Resuelve** parte de Q-O.
- **Datos** (`Assets/_Project/Data/`): `Levels/Level_01..05` (`LevelData`), `Levels/LevelCatalog`, `Dialogues/Dialogue_01..05` y `Dialogue_Epilogue` (`DialogueData`). Canciones en `Resources/Songs/Song_01..05` (`SongData`, cargadas por nombre desde `LevelData.SongResource`).
- **Arco:** 1 "Comienzos" (el cuarto), 2 "Sueños Punk Rock" (garaje y primeros recitales; decisión de dificultad), 3 "Peatonal Córdoba" (música callejera y transeúntes; decisión de variante de escenario), 4 "Facultad de Medicina" (escalinatas, autogestión, purista; decisión de dificultad), 5 "Lucha Interior" (clímax; decisión de dificultad).
- **Charts placeholder con dificultad creciente:** 100 → 140 BPM, 56 → 96 beats y notas a contratiempo cada vez más frecuentes (`SongData._testOffbeatEvery`): 56, 72, 90, 120 y 192 notas; todas las canciones duran más de 28 s (hay al menos un debuff). El audio es un metrónomo placeholder por nivel (`Audio/Placeholders/LevelN_xxxBPM.wav`).
- **Validación:** el builder comprueba que cada nivel tenga narrativa, canción, audio y chart (`[Levels] Los 5 niveles enlazan...`) y se verificó por script (títulos, líneas, notas, última nota dentro del audio, BPM y densidad crecientes).
- **Nota editorial:** los textos son un BORRADOR escrito solo con lo que dice el GDD (lugares, personajes, tono). Hay que revisarlos contra la historia real antes de publicar; no se narra el final trágico.

### ADR-023 — Cierre del loop: nivel 5 → epílogo → créditos → menú
- **Estado:** Aceptada · **Fecha:** 2026-11-09
- **Decisión:** nuevo estado `Epilogue`. Al ganar el último nivel, Resultados muestra el botón "EPILOGO" (`ResultsData.isFinalLevel`) que abre el epílogo (`PreLevelDialoguePresenter.BeginDialogue("EPILOGO", DialogueData)`); al terminarlo, `ProgressStore.MarkGameCompleted` (marca `gameCompleted` y deja los 5 niveles desbloqueados) y se muestran los Créditos (`CreditsScreen` también en la escena Gameplay); "Volver" carga la escena MainMenu con el `SceneLoader`.
- **Transiciones:** `ResultsScreen → Epilogue → Credits`; `Gameplay` no salta directo al epílogo.
- **Verificado por script:** Resultados (nivel 5) → EPILOGO → líneas → Créditos; `MarkGameCompleted` deja 5 niveles abiertos; `SetCalibration` y `StartNewGame` se comportan según lo esperado.
- **Pendiente:** créditos finales definitivos, música del epílogo y un posible desbloqueo de contenido extra tras completar el juego.

<!-- Añadir ADR-024+ debajo -->

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
├── Scenes/               # (vacía; ver _Project/Scenes)
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
| Judgement (M02) | Ventanas Perfect/Good/Miss | 🟩 Validado en Play Mode |
| Puntaje / Combo / Vida (Hito 2) | `ScoreSystem`, `HealthSystem`, `RhythmSession`, `RhythmHudPresenter` | 🟨 Compila; falta validar en Play Mode |
| Modo Punk (Hito 3) | `PunkModeSystem`, ventanas permisivas, barra en HUD | 🟨 Lógica verificada en Editor (22 pruebas); falta Play Mode |
| Debuffs (Hito 3) | `DebuffSystem`, `RhythmDebuffVisuals`, opción de accesibilidad | 🟨 Lógica verificada en Editor; efecto visual sin ver en Play Mode; alteración del chart pendiente |
| NPCs saboteadores | `NpcSabotageController`, aviso `Incoming`, escudo y marco dentado | 🟨 Lógica verificada en Play Mode por script (impacto con marco, absorción con escudo); animación sin ver en vivo; arte placeholder |
| Narrativa pre-nivel | `DialogueData`, `PreLevelDialoguePresenter`, decisiones → `GameSession` | 🟨 Recorrido verificado por script (mecanografiado, retrato, etiqueta final, paso a preparación) |
| Calibración / Tutorial | `CalibrationScreen`, `DspRealtimeMapper`, persistencia de latencia | 🟨 Cálculo verificado por script (+25.9 ms con 25 simulados); falta probar con teclado y audio reales |
| Navegación / Flujo de pantallas (Hito 4) | `GameStateMachine`, `MenuFlowController`, `GameplayFlowController`, `ScreenManager`, 9 pantallas, `LevelCatalog`, `LevelData`, `ProgressStore`, `GameSession` | 🟨 Compila; verificado por script: Jugar → selector (nivel 2 bloqueado) y Nivel 1 → carga asíncrona de Gameplay. Falta recorrer Gameplay completo con clics reales |
| Escenas / SceneLoader | `MainMenu.unity`, `Gameplay.unity`, `SceneLoader` | 🟨 Generadas y en Build Settings; transición menú → Gameplay verificada |
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
- 2026-11-09 · [Rhythm/Flow] · Deuda técnica, 5 niveles y cierre del juego (ADR-021, ADR-022, ADR-023): se elimina `DspRealtimeMapper` y `SongClock` pasa a ser la única conversión DSP↔tiempo real (`InputRealtimeToDsp`, `SampleNow`, `ResetTimebase`); "Recalibrar latencia" en Opciones; `ProgressStore.SetCalibration/MarkGameCompleted` y `gameCompleted`; estado `Epilogue`; `GameplayFlowController` deriva el nivel 5 a epílogo → créditos → menú; `ResultsScreen` con botón EPILOGO; 5 `LevelData` + `DialogueData` + `SongData` (charts y audio placeholder por nivel) + epílogo; `SongData._testOffbeatEvery`; builder `RhythmLevelContentBuilder` con validación de niveles.
- 2026-11-09 · [Rhythm/Flow] · NPCs saboteadores, narrativa formal y primera partida (ADR-018, ADR-019, ADR-020): `NpcSabotageController`, `DebuffSystem.Incoming/Lead`, `DialogueData` + `PreLevelDialoguePresenter` (se elimina `PreLevelScreen`), `CalibrationScreen`, `DspRealtimeMapper`, `LaneStyle`, `GameSettings.LatencyOffsetMs`, `ProgressData.calibrated/latencyOffsetMs`, estado `Calibration`, `MainMenuScreen` con PARTIDA NUEVA y confirmación, `GameSession` con dificultad y variante, `RhythmTrackController.SetSpeedMultiplier`. Builders: `RhythmFxBuilder` (sprites y diálogos), `RhythmCalibrationBuilder`. Assets nuevos: `Art/Placeholders` (NPC, proyectil, escudo, marco, retrato), `Audio/Placeholders/Calibration_Click.wav`, `Data/Dialogues`.
- 2026-11-09 · [Rhythm/Flow] · Hito 3 y cierre de mecánicas (ADR-015, ADR-016, ADR-017): `PunkModeSystem`, `DebuffSystem`, `RhythmDebuffVisuals`; `RhythmSession` integra Modo Punk y debuffs; `RhythmTrackController` suma ventanas permisivas (`SetPermissiveWindows`), `SongStopped` y `LoadSong` sin reconstruir si es la misma canción; `RhythmHudPresenter` pinta la barra Punk; controles A-S-D-F + flechas con etiquetas bajo los receptáculos; botón OPCIONES en Game Over; `GameSettings` movido a `DiosPunk.Rhythm`; debug F5 (Modo Punk) y F6 (debuff). El builder de escenas genera todo.
- 2026-11-09 · [Flow/Scenes] · Arquitectura multiescena (ADR-013) y fix de clics de UI (ADR-014): escenas `MainMenu` y `Gameplay` en `Assets/_Project/Scenes/` (SampleScene renombrada a Gameplay), `SceneLoader`, `GameSession`, `LevelCatalog`, `MenuFlowController`, `GameplayFlowController`, `FlowControllerBase`, `CreditsScreen`. `GameFlowController` eliminado. `LevelData` carga la canción por `Resources/Songs`; `TestSong_120BPM.asset` movido a `Assets/_Project/Resources/Songs/`. Builder: `DiosPunk/Scenes/Build MainMenu + Gameplay` (reemplaza `DiosPunk/Flow/Build Screens`). El objeto vacío "prueba claude" se eliminó al regenerar la escena.
- 2026-11-08 · [Flow] · Userflow y gestión de pantallas (ADR-011, ADR-012): assembly `DiosPunk.Flow` (`GameStateMachine`, `GameFlowController`, `ScreenManager`, `ScreenPanel`, `LevelData`, `ProgressStore`, `GameSettings` y 8 pantallas), `ScoreFormatter`, `RhythmTrackController.LoadSong`. Builder `DiosPunk/Flow/Build Screens` (crea 5 `LevelData` placeholder, pantallas, EventSystem con Input System UI y `GameFlow`). Compila sin errores; Play Mode arranca en el menú sin errores.
- 2026-11-08 · [Rhythm] · Hito 2: `ScoreSystem`, `HealthSystem`, `RhythmSession`, `RhythmHudPresenter` (ADR-009). `NoteView.Init` recibe el color del carril; `RhythmTrackController` expone `GetLaneColor`. `RhythmDebugTools` agrega puntaje/precisión/vida al resumen. `DiosPunk.Rhythm.asmdef` referencia `UnityEngine.UI`.
- 2026-11-08 · [Art/Scene] · Placeholders alineados al GDD (ADR-010): highway, carriles de color, receptáculos translúcidos y HUD (paneles 9-slice, barra de vida, medidor de Modo Punk vacío). El builder pasa a crear `HudCanvas` (renombra el `DebugCanvas` anterior) y `RhythmHudPresenter`. Compila sin errores; el HUD en Play Mode no se validó.
- 2026-11-08 · [Docs] · Incorporado el GDD completo de la entrega E1: hoja de ruta por hitos (§10) y preguntas Q-I, Q-J.
- 2026-11-04 · [Git] · Reglas LFS (audio/texturas) y normalización de YAML de Unity · `.gitattributes`

---

## 9. Preguntas abiertas / riesgos

| # | Pregunta o riesgo | Estado |
|---|---|---|
| Q-A | Notas "injustas" del debuff | **Resuelta:** patrones densos definidos a mano por canción en `SongData` |
| Q-B | Modo Punk: umbrales, tasas de carga, duración, ampliación de ventana | **Resuelta (2026-11-09):** +15 % por Miss, +5 % por racha de 10, 100 % activa, drena en 9 s, ventanas ±45/±90 ms (ADR-015) |
| Q-C | Juicio | **Resuelta (actualizada, ver ADR-007):** Perfect ±30 / Good ±70 / Miss. Puntaje/precisión: pendiente |
| Q-D | Vida: máximo, daño por fallo, recuperación | Abierta → provisional en SO de config (100 HP, −10 por Miss, +1 por acierto) |
| Q-E | Notas hold | **Resuelta:** solo notas simples en MVP; hold queda para ampliación |
| Q-F | Guardado | Abierta → provisional: JSON en `persistentDataPath` (nivel desbloqueado, mejores resultados, ajustes) |
| Q-G | Teclas | **Resuelta:** D-F-J-K y flechas activos a la vez; sin rebindeo en MVP |
| Q-H | La bitácora de la entrega E1 menciona consultas al mentor sobre middleware de audio (integración Unity/Wwise). El MVP usa `AudioSource` nativo (ADR-001): ¿se migra a Wwise más adelante? Si sí, `SongClock` deberá abstraer la fuente de tiempo de audio | Abierta |
| R-1 | Latencia de audio variable en PC → calibración obligatoria | Riesgo |
| R-2 | Debuffs de shader en 120 Hz: presupuesto de GPU del pase fullscreen en URP 2D | Riesgo |
| Q-I | Notas sostenidas (hold) | **Resuelta (2026-11-08):** fuera del MVP; arquitectura extensible |
| Q-J | Estética del GDD (carriles de color, multiplicador, HUD) | **Resuelta (2026-11-08):** aplicada con placeholders (ADR-010). Pendiente: marcos irregulares, NPCs y proyectiles (arte/Hito 3) |
| Q-K | Escalones del multiplicador y formato del puntaje | **Resuelta (2026-11-08):** +1 cada 10 aciertos hasta x4; separador de miles con punto |
| Q-L | Diagrama de Figma del userflow no disponible: el flujo se implementó según el texto del GDD. Pendiente validar ramas no descritas, la calibración de latencia + tutorial de primera partida y el epílogo/créditos | Abierta |
| Q-M | Botón "Jugar" va directo al selector (sin "partida nueva"/calibración/tutorial) y "Salir" de la pausa vuelve al selector; ajustes de Opciones sin persistir y `ReduceVisualEffects` aún sin efecto | Abierta |
| Q-P | Las canciones de los 5 niveles son metrónomos placeholder con charts generados; faltan las canciones definitivas (re-grabadas, GDD) y sus charts autorados, además de la alteración del chart por debuff (Q-A) | Abierta |
| Q-O | El guion real de las 5 escenas narrativas (hablantes, textos, retratos y decisiones con sus consecuencias) sigue pendiente; hoy hay texto placeholder. Las decisiones solo cambian velocidad (dificultad) y la etiqueta de variante de escenario; falta arte por variante | Abierta |
| Q-N | La preparación de canción (título, escenario, dificultad, Comenzar) quedó en la escena Gameplay, junto a la narrativa pre-nivel. Sin pantalla de carga visual ni fundido entre escenas (solo `SceneLoader.ProgressChanged`). Las canciones definitivas deberán vivir en `Resources/Songs` (o migrar a Addressables) | Abierta |

---

## 10. Hoja de ruta de la entrega E1 (según el GDD entregado)

| Hito | Contenido | Estado |
|---|---|---|
| 1 (semana 1) | Repo + Git LFS + convenciones; M01 Core de Ritmo; M02 Input/Hit; escenario de pruebas con placeholders y metrónomo 120 BPM | Hecho (validado en Play Mode) |
| 2 (semana 2) | Puntuación ponderada, multiplicador de combo, barra de vida (100 HP, penalización por Miss, curación leve por acierto); VFX de impacto y feedback auditivo | Pendiente |
| 3 (semana 3) | Modo Punk (barra dual) y Debuffs periódicos (25 s / 5 s) con absorción por escudo Punk | Pendiente |
| 4 (semana 4) | Narrativa pre-canción con ScriptableObjects; userflow completo (menú, selector, resultados) y persistencia JSON local | Pendiente |

**Contexto de audio (GDD):** canciones re-grabadas, "guitarra jugable" con dos procesos de audio (modo normal y modo punk), SFX con frases originales de Javier, ambientes de la peatonal Córdoba y la Facultad de Medicina UNR. El plan técnico de esta fase excluye librerías de audio intermedias (ver Q-H).
