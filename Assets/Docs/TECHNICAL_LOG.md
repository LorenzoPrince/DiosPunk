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
- **Estado:** Aceptada (la carga por rachas de 10 fue reemplazada por carga continua en ADR-024) · **Fecha:** 2026-11-09 · **Resuelve** Q-B.
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

### ADR-024 — Modo Punk: carga continua por acierto (ajuste tras pruebas reales)
- **Estado:** Aceptada · **Fecha:** 2026-11-09 · **Reemplaza** la carga por rachas de 10 aciertos de ADR-015.
- **Problema:** la barra solo se movía en bloques tras rachas largas; el jugador no veía respuesta inmediata.
- **Decisión:** cada acierto suma de inmediato: Perfect +1,0 %, Good +0,5 %; Miss mantiene el rescate rápido de +15 %. `PunkModeSystem.Configure(miss, perfect, good, segundos)` (se eliminan `StreakCharge`/`StreakSize`); `RhythmSession` expone `_punkPerfectCharge` y `_punkGoodCharge`. Con el modo activo la barra no se carga, solo se drena. Se agregó una tolerancia de 0,01 % al llegar a 100 % (100 × 0.01f en float no alcanza 1.0).
- **Balance resultante:** 100 Perfect, 200 Good o 7 Miss activan el modo; el nivel 1 (56 notas) no lo llena solo con aciertos: sigue siendo recompensa lenta para expertos y rescate rápido para novatos.
- **Verificado por script:** 1 % por Perfect y 0,5 % por Good con `BarChanged` en cada acierto, activación exacta en el acierto 100/200, y sin carga durante el modo activo.

### ADR-025 — Impacto del debuff: "tomatazo" y cartel de debuff absorbido
- **Estado:** Aceptada · **Fecha:** 2026-11-09
- **Sin Modo Punk:** al impactar el proyectil, `NpcSabotageController` muestra una mancha roja irregular (sprite `TomatoSplat_Placeholder`, 512 px, generado por código: cuerpo ruidoso con puntas, salpicaduras y goteos) centrada en la pantalla, con alpha 0,72 (0,2 con "Reducir efectos"). Entra con un breve "estrellarse" (pop de 0,18 s), dura los 5 s del debuff y se desvanece en el último segundo usando el tiempo de canción (se congela en pausa). Los proyectiles ahora son rojos/naranjas. El velo de pantalla de `RhythmDebuffVisuals` pasó a rojo oscuro tenue (alpha 0,2) para no teñir la mancha.
- **Con Modo Punk:** destello dorado de pantalla completa (0,3 s), el escudo y un cartel estilo fanzine inclinado "¡DEBUFF ABSORBIDO! / El Escudo Punk te salvó" (1,8 s, con pop de entrada y salida rápida). `RhythmDebuffVisuals` ya no muestra su mensaje de texto de absorción.
- **Sin allocations:** todo por eventos; el Update solo corre mientras hay un efecto activo.
- **Verificado en Play Mode por script y con capturas:** impacto → mancha visible, marco dentado y proyectil; absorción → cartel, destello y escudo, sin mancha ni debuff.

### ADR-026 — Notas más grandes y carriles más anchos
- **Estado:** Aceptada · **Fecha:** 2026-11-09
- **Decisión:** nota de 1,20 × 0,40 u a **1,56 × 0,52 u (+30 %)** con borde de 5 px (antes 4). Para que la nota más ancha no toque el carril vecino la separación de carriles pasa de 1,5 a **1,9 u** (highway de 7,6 u; HitBar de 760 px). Receptáculos y pads usan el mismo sprite, así que crecen en proporción.
- **Verificado:** nota 1,56 × 0,52 (+30 % ancho y alto) y 1,56 < 1,9.

### ADR-027 — Curva de dificultad: nivel 2 más suave
- **Estado:** Aceptada · **Fecha:** 2026-11-09
- **Problema:** el paso del nivel 1 (100 BPM) al nivel 2 (110 BPM, misma velocidad de caída y contratiempos desde el inicio) era un salto abrupto.
- **Decisión:** `LevelData._noteSpeedMultiplier` (velocidad de caída por nivel, multiplicada por el ajuste de dificultad de la narrativa) y `SongData._testWarmupBeats` (beats iniciales a media densidad, sin contratiempos). Nivel 2: warm-up de 16 beats, contratiempo cada 12 beats (antes 8) y velocidad ×0,90. Para una progresión sin saltos: N1 ×1,00, N2 ×0,90, N3 ×0,95, N4 ×1,00, N5 ×1,05.
- **Números (notas/s, velocidad u/s):** N1 1,60 / 8,0; N2 1,65 / 7,2; N3 2,40 / 7,6; N4 3,12 / 8,0; N5 4,50 / 8,4. En los primeros 16 beats el N1 tiene 16 notas y el N2 8. El tiempo de lectura sube (lookahead de 1,44 s a 1,60 s en el N2).
- **Pendiente:** validar el balance con jugadores; los charts siguen siendo generados (Q-P).

### ADR-028 — Indicador de progreso de la pista en el HUD
- **Estado:** Aceptada · **Fecha:** 2026-11-09
- **Decisión:** `SongProgressPresenter` (en el panel `SongProgressPanel`, arriba al centro bajo la barra de vida): cinta con fill de tinta y aguja roja que avanza, más el contador "mm:ss / mm:ss". Lee `SongClock.SongTime` (dspTime) contra `RhythmTrackController.DurationSeconds` (instante de la última nota + 0,1 s: el nivel termina al resolverse la última nota, así la cinta llega al 100 % justo al final y no queda un tramo de audio sobrante).
- **Sin garbage:** cada frame solo se actualizan `fillAmount` y `anchoredPosition` de la aguja; el texto se reescribe únicamente cuando cambia el segundo entero (o la duración), con `TimeFormatter.FormatProgress` sobre un `char[]` preasignado y `TMP_Text.SetCharArray`. El tiempo del lead-in (negativo) se muestra como 00:00. Tras terminar o detener la canción la cinta queda donde estaba; se reinicia con `SongStarted`.
- **Layout:** la barra de juicio (`JudgementText`) bajó a y = −185 y `DebuffText` a y = −260 para no pisar la cinta. El panel se oculta con el resto del HUD fuera de Gameplay/Pausa/Game Over.
- **Verificado:** formato de tiempo (4 casos) y captura del HUD en Play Mode ("00:00 / 00:35", aguja al inicio). El avance en vivo no se pudo ver con Unity sin foco.

### ADR-029 — Rótulo de aclaración en los NPC placeholder
- **Estado:** Aceptada · **Fecha:** 2026-11-09
- **Decisión:** cada NPC lateral lleva un `TextMeshPro` hijo ("PlaceholderLabel") sobre el torso con "PLACEHOLDER / REEMPLAZAR POR DIBUJO REAL" en blanco y negrita (auto-ajuste de tamaño al ancho del torso). Al ser hijo del NPC acompaña el desplazamiento del lanzamiento; como la animación es solo traslación (sin rotación) el texto mantiene su orientación. El NPC derecho se espeja con `SpriteRenderer.flipX` en vez de escala negativa (que habría invertido el texto). Es solo un indicio para revisiones: se elimina junto con los sprites al reemplazarlos por el arte real.

### ADR-030 — Feedback de absorción fuera de los carriles (ajuste tras pruebas reales)
- **Estado:** Aceptada · **Fecha:** 2026-11-09 · **Modifica** la parte de absorción de ADR-025.
- **Problema:** el cartel "¡DEBUFF ABSORBIDO!" y el destello dorado cubrían el centro de la pista y tapaban las notas, provocando fallos por no ver el chart.
- **Cartel:** pasó a un sello compacto (440 × 96 px, título de 32 pt) en la esquina superior izquierda, justo bajo el marco de MODO PUNK (x 36–476 px de 1920; los carriles empiezan cerca de x 550), inclinado −3°. Los 4 carriles quedan 100 % visibles.
- **Temporización:** duración total 1,2 s (`_bannerSeconds`): entrada rápida (0,1 s a 1,1× y 0,08 s para asentarse) y desvanecimiento suave en los últimos 0,35 s con `CanvasGroup`. El destello dura 0,18 s y el anillo del escudo 0,35 s (`_absorbEffectSeconds`).
- **Destello y anillo:** se eliminó la capa de pantalla completa; ahora es un resplandor radial pequeño (420 × 110 px, alpha 0,3, sprite `ShieldGlow_Placeholder`) bajo la HitBar. El anillo del escudo se redujo de 3,6 × 1,6 a 2,4 × 0,55 u, alpha 0,6, y se movió a y = −4,35 (la HitBar está en −3,5); el punto de impacto del proyectil también bajó a y = −4,35.
- **Sin allocations:** solo se tocan `localScale`, `CanvasGroup.alpha` y colores en el Update mientras dura el efecto.
- **Verificado en Play Mode con captura:** el sello queda a la izquierda bajo MODO PUNK y el destello/anillo bajo la HitBar, sin cubrir carriles ni notas. El desvanecimiento en vivo no se pudo ver con Unity sin foco.

### ADR-031 — Sistema central de SFX (`SoundEffectManager` + `SfxLibrary`)
- **Estado:** Aceptada · **Fecha:** 2026-11-09 · Cubre el pipeline de audio del GDD (efectos).
- **Servicio global:** `SoundEffectManager` (asmdef `DiosPunk.Rhythm`) se crea solo con `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` en un GameObject `DontDestroyOnLoad`; no hay que agregarlo a ninguna escena. Uso: `SoundEffectManager.Play(SfxId.X, volumeScale, delaySeconds)`. Si falta la biblioteca avisa una vez y queda en silencio.
- **Canales:** pool fijo de 12 `AudioSource` creado al iniciar. Cada `Play` usa la primera voz libre (o roba la más antigua); sin instanciar ni generar garbage. Pitch aleatorio por efecto (rango en la biblioteca) para que las repeticiones no suenen idénticas.
- **Volumen independiente:** `GameSettings.SfxVolume` (0..1) lo controla el nuevo slider "Volumen de efectos (SFX)" de Opciones (`OptionsScreen._sfxVolume`, pestaña Audio, en ambas escenas); al arrastrarlo suena un acierto de muestra (máx. cada 0,15 s). Sigue existiendo el "Volumen general" (`AudioListener.volume`). No se persiste todavía (igual que el resto de los ajustes salvo la latencia).
- **Biblioteca:** `SfxLibrary` (ScriptableObject en `Resources/Audio/SfxLibrary.asset`) con un `SfxEntry` (clips[], volumen, pitch mín/máx) por categoría: UI (`UiClick`, `UiHover`), juicios (`HitPerfect`, `HitGood`, `Miss`), proyectiles/debuffs (`NpcThrow`, `TomatoImpact`, `ShieldBreak`) y Modo Punk (`PunkActivate`, `PunkShout`, con varias variantes elegidas al azar para las arengas de Javier). El área de sonido solo arrastra los clips definitivos (.wav/.ogg) al asset.
- **Placeholders procedurales:** `RhythmAudioBuilder` sintetiza 11 WAV mono 44,1 kHz en `Audio/Placeholders/Sfx/` (papel/fanzine para la UI, caja para los aciertos, zumbido que se corta para el Miss, whoosh, splat con burbujas, metal inarmónico para el escudo, acorde de potencia distorsionado, dos "arengas" vocales con formantes). Semillas fijas: regenerar produce los mismos bytes. El builder solo crea los WAV que faltan y **solo rellena un campo de la biblioteca si está vacío o contiene placeholders**, así que correr de nuevo el generador de escenas no pisa audio definitivo (para forzar: menú `DiosPunk/Audio/Regenerate SFX placeholders`).
- **Conexión (por eventos, sin polling):** `GameplaySfxBridge` (en `RhythmTrack`) escucha `NoteJudged`, `Debuffs.Incoming/Started/Absorbed` y `Punk.Activated`; `RhythmSession` y los sistemas puros no conocen el audio. El lanzamiento suena en `Incoming`, el tomatazo en `Started`, la rotura del escudo en `Absorbed`; la activación del Modo Punk dispara el efecto y la arenga con 0,15 s de retraso (`PlayDelayed`).
- **UI:** `UiSfxBinder` (en `MenuCanvas` y `HudCanvas`) agrega en `Awake` un `UiSfxTrigger` a cada `Selectable` (botones, toggles, sliders; incluidos los de pantallas inactivas). Hover con puntero o navegación (`IPointerEnterHandler`, `IMoveHandler`) y clic/submit (`IPointerClickHandler`, `ISubmitHandler`); respeta `interactable`.
- **Verificado:** el generador valida la biblioteca (11 clips en 10 categorías) y corre sin errores; en Play Mode `SoundEffectManager` queda listo y el menú tiene 30 triggers. **No se escuchó el audio** (sin salida de sonido en la verificación): el mix y los volúmenes iniciales son una primera pasada que el equipo debe ajustar de oído.

### ADR-032 — Doble pista para el Modo Punk (stem sincronizado)
- **Estado:** Aceptada · **Fecha:** 2026-11-09 · Cubre el pipeline de audio del GDD (dos mezclas).
- **Datos:** `SongData` gana `_punkStemClip` (opcional): la segunda mezcla con guitarra distorsionada / furia. Debe coincidir con la base en frecuencia, duración y alineación; `ValidateLevels` del generador exige igual cantidad de muestras y frecuencia, y `SongClock` avisa si no coinciden.
- **Reloj:** `SongClock` tiene un segundo `AudioSource` (`_punkSource`; si falta se crea en `Awake`). `Play(clip, punkStem)` programa **ambos con `PlayScheduled` sobre el mismo `dspTime`**, así que arrancan sample-accurate. `Pause` detiene ambos y `Resume` reprograma ambos desde la misma posición (`timeSamples` de cada clip según su frecuencia) y el mismo instante DSP. La línea de tiempo (`SongTime`, `JudgementTime`, conversiones de input) no cambia: sigue dependiendo solo de `dspTime`.
- **Mezcla:** con el Modo Punk apagado el volumen del stem es 0 y el de la base 1. `SetPunkStemActive(bool)` fija el objetivo y el `Update` del reloj hace el crossfade lineal (`_punkCrossfadeSeconds`, 0,25 s por defecto) con `MoveTowards` y tiempo sin escalar; solo trabaja mientras hay una transición en curso (cero garbage). `Stop` y un `Play` nuevo vuelven a la mezcla base al instante. Sin stem cargado la base queda a pleno y no pasa nada.
- **Integración:** `RhythmSession` ya reaccionaba a `Punk.Activated/Deactivated` para las ventanas permisivas; ahora además llama `Clock.SetPunkStemActive` (se activa al llegar al 100 % y vuelve a la mezcla base al agotarse o al absorber un debuff).
- **Placeholders:** `RhythmAudioBuilder.WritePunkStemWav` genera por nivel (`LevelN_xxxBPM_PunkStem.wav` y `Metronome_120BPM_PunkStem.wav`) una mezcla con un golpe de acorde de potencia distorsionado en cada beat (raíz distinta por nivel) y un platillo en cada compás, con la misma fórmula de muestras que el metrónomo base (largo idéntico).
- **Verificado:** los 5 stems tienen exactamente las mismas muestras y frecuencia que su base (validación del generador). En Play Mode por script: con stem, `blend = 0`, base 1 / punk 0 y los dos clips asignados; `Stop` o sin stem vuelven a base 1 / punk 0. **No se pudo ver el crossfade en vivo** (Unity sin foco no avanza frames) **ni escucharlo**.

### ADR-033 — Cortinilla de carga fanzine en `SceneLoader`
- **Estado:** Aceptada · **Fecha:** 2026-11-09 · Modifica ADR-013 (carga de escenas).
- **Overlay:** `SceneTransitionOverlay` (prefab `Resources/UI/SceneTransition.prefab`, generado por el builder): canvas Screen Space Overlay con orden 32000 y `CanvasGroup`; fondo casi negro con grano de fotocopia en mosaico (`PaperGrain_Placeholder.png`, ruido determinista, `Image.Type.Tiled`), tira de papel torcida con "CARGANDO…" y una cinta de progreso con relleno rojo. El rótulo cicla entre cuatro strings constantes y la tira "tiembla" 0,8° cada 0,3 s (sin allocations). Mientras se ve bloquea los clics a la UI de abajo; con alpha 0 se apaga el canvas.
- **Secuencia del `SceneLoader`:** (1) fundido de entrada de 0,25 s a opaco antes de tocar nada; (2) `LoadSceneAsync` con `allowSceneActivation = false`, progreso 0–0,9 mapeado a la cinta y cortinilla visible al menos 0,45 s para evitar parpadeos con cargas instantáneas; (3) se activa la escena, se espera `isDone`, `Resources.UnloadUnusedAssets` + `GC.Collect()` detrás de la cortinilla; (4) fundido de salida de 0,35 s. `IsLoading` se mantiene hasta que termina el fundido y `Loaded` se dispara al final. Todo con tiempo sin escalar.
- **Robustez:** el overlay se instancia una sola vez dentro del GameObject `DontDestroyOnLoad` del loader. Si el prefab no existe, `SceneLoader` avisa y carga en seco como antes. Si `LoadSceneAsync` devuelve null (escena fuera de Build Settings) se destapa la pantalla y se libera `IsLoading`.
- **Verificado:** el prefab se renderizó a una textura (grano, tira, texto, cinta al 60 %) y `SceneLoader.Load` instancia el overlay y empieza el fundido en Play Mode. **No se pudo ver la transición completa** MainMenu ↔ Gameplay (sin foco no avanzan frames): falta probarla jugando y ajustar tiempos.

### ADR-034 — Ventanas de timing permisivas, juicio "Bad" y vida más suave (feedback v0.2.0)
- **Estado:** Aceptada (código escrito; **sin compilar ni probar**, ver nota) · **Fecha:** 2026-11-16 · Modifica ADR-007 y ADR-015.
- **Problema:** los playtests reportan ventanas muy severas y un Nivel 2 demasiado difícil.
- **Ventanas simétricas:** estándar Perfect ±55 ms (antes ±30), Good ±115 (antes ±70); Modo Punk Perfect ±75, Good ±145 (`HitWindows.Default` y `HitWindows.Punk`).
- **Juicio nuevo `HitType.Bad`:** pulsar entre el borde de Good y el borde exterior (±155 ms estándar, ±185 con Modo Punk), temprano **o tarde**, consume la nota con un Bad: sin puntos, rompe el combo, cuenta como fallo en la precisión y los resultados, resta solo 5 HP (`BadDamage`) y da media carga de rescate al Modo Punk. El campo `earlyMissMs` pasa a `badMs` (`FormerlySerializedAs`). Una nota sin pulsar se declara Miss recién al pasar `badMs` (antes `goodMs`), así que el Miss tardío se detecta ~40 ms después. Un input más temprano que `badMs` se sigue ignorando (sin castigo por tap fantasma). Se agregó Bad al HUD de debug, al color de los pads (naranja), a `RhythmStats` y al SFX (sonido de Miss al 60 %).
- **Vida:** Miss −10 HP (antes −15), Bad −5, Perfect +3 (antes +2), Good +1,5 (antes +1) (`HealthSystem` y `RhythmSession`).
- **Nota:** los valores serializados en la escena `Gameplay` y el prefab de la pista viven en la escena: hay que **regenerar** (`DiosPunk/Scenes/Build MainMenu + Gameplay`) para que tomen los nuevos defaults.

### ADR-035 — Latencia por defecto de +25 ms y calibración robusta
- **Estado:** Aceptada (código escrito; sin compilar ni probar) · **Fecha:** 2026-11-16 · Modifica ADR-021.
- **Defecto:** `GameSettings.DefaultLatencyOffsetMs = 25` y `LatencyOffsetMs` arranca en ese valor; `SongClock.Awake` lo toma siempre (antes ignoraba 0). Al omitir la calibración se aplica el defecto (antes 0). Un jugador ya calibrado conserva su valor de `progress.json`.
- **Signo (sin cambios, verificado por lectura):** el offset es positivo si el jugador pulsa tarde; `InputRealtimeToSongTime` lo **resta** del tiempo del input (`input + dspMinusRealtime − songStart − offset`) y `JudgementTime` lo resta también, así que +25 adelanta el input 25 ms. `InputRealtimeToDsp` (usado por la calibración) no aplica offset: mide el desfase crudo.
- **Calibración:** `CalibrationScreen` descarta pulsaciones a más de 250 ms del clic más cercano (no cuentan para el objetivo de 10) y calcula la **mediana**; sobre ella recorta lo que quede a más de 80 ms y promedia el resto (si quedan menos de 4 usa todas). Muestra cuántas descartó. Arrays preasignados; el cálculo ocurre una vez al terminar.

### ADR-036 — Revelación progresiva de mecánicas por nivel
- **Estado:** Aceptada (código escrito; assets de nivel pendientes de regenerar) · **Fecha:** 2026-11-16 · Modifica ADR-027.
- **Flags en `LevelData`:** `_enableDebuffs`, `_enablePunkMode`, `_firstDebuffSeconds`, `_debuffIntervalSeconds`. `GameplayFlowController.ApplyLevelMechanics` los pasa a `RhythmSession.ConfigureLevel` (`DebuffSystem.Enabled/FirstTrigger`, `PunkModeSystem.Enabled`) antes de cada canción y oculta el panel MODO PUNK (`CanvasGroup`) si el nivel no lo usa. F5/F6 de debug siguen funcionando.
- **Curva (generada por `RhythmLevelContentBuilder`):**

| Nivel | Velocidad | Debuffs | Modo Punk | Densidad |
|---|---|---|---|---|
| 1 Comienzos | ×0,80 | no | no (ritmo puro) | 1ª mitad a media densidad, sin contratiempos |
| 2 Sueños Punk Rock | ×0,85 | primero a los 30 s, luego cada 30 s | sí (salvavidas) | 1ª mitad a media densidad, sin contratiempos |
| 3 Peatonal Córdoba | ×0,92 | cada 28 s | sí | sin cambios |
| 4 Facultad de Medicina | ×1,00 | cada 25 s | sí | sin cambios |
| 5 Lucha Interior | ×1,08 | cada 22 s | sí | sin cambios |

- **Decisiones mías a validar:** el Modo Punk se apaga también en el nivel 1 (el pedido solo pide quitar debuffs, pero "ritmo puro" y "activar Modo Punk como salvavidas" en el 2 lo sugieren); el intervalo del nivel 2 tras el primer debuff es 30 s (no estaba especificado). Con las duraciones actuales (~35–45 s) cada nivel 2–5 recibe un solo debuff: si se quiere más presión, hay que alargar los charts.
- **Pendiente de verificar:** compilación, regeneración de escenas y niveles, y Play Mode. El MCP de Unity quedó **revocado** durante esta tarea ("Connection revoked"), así que no se pudo compilar ni ejecutar el generador.

### ADR-037 — Esquema de teclas configurable (`ControlScheme`)
- **Estado:** Aceptada (código escrito; **sin compilar ni regenerar escenas**: el MCP de Unity se revocó otra vez durante la tarea) · **Fecha:** 2026-11-16 · Modifica ADR-017 (controles).
- **Enum:** `ControlScheme { Asdf, Dfjk, Arrows, All }` (por defecto `All`) con tablas estáticas en `ControlSchemes` (rótulos por carril, descripción, nombres, bindings). `GameSettings.ControlScheme` + evento `ControlSchemeChanged`.
- **Input sin allocations:** `LaneInputSet` crea una sola vez 12 `InputAction` (4 carriles × 3 juegos de teclas: ASDF, DFJK, flechas). Cambiar de esquema solo habilita/deshabilita las acciones del esquema (`All` = ASDF + flechas) al recibir el evento; no hay rebindeo ni garbage en caliente, y los timestamps siguen siendo `CallbackContext.time`. `RhythmInputReader` y el tutorial de `CalibrationScreen` usan el mismo `LaneInputSet`, así que se actualizan en vivo (también desde Pausa).
- **Persistencia:** `ProgressData.controlScheme` (int, defecto 3 = Todos: un `progress.json` viejo sin el campo queda en Todos). `GameSession.GetProgress` lo aplica al cargar y `GameSession.SetControlScheme` lo aplica y guarda. **Detalle:** si todavía no existe `progress.json`, la preferencia queda en memoria y se escribe con el primer guardado, porque crear el archivo antes haría que JUGAR lo tome por una partida guardada y se saltee la calibración.
- **Opciones (pestaña Controles, en MainMenu y en Pausa):** título "Esquema de teclas", valor grande (ASDF / DFJK / FLECHAS / TODOS) con botones « » y detalle de teclas; también cambia con las flechas Izquierda/Derecha mientras la pestaña está visible (`InputAction` propia habilitada solo en esa pestaña). Guarda al instante.
- **Rótulos:** `LaneKeyLabels` (en `RhythmTrack`) actualiza los 4 textos bajo los receptáculos por evento ("D / F / J / K", flechas, "A / S / D / F" o "A / ← …"). El tutorial de calibración muestra las teclas del esquema en cada carril y en el texto de progreso (`CalibrationScreen._laneKeyLabels`).
- **Pendiente:** compilar, regenerar con `DiosPunk/Scenes/Build MainMenu + Gameplay` y probar con teclado: cada esquema debe ignorar las teclas de los otros, y el cambio desde Pausa debe aplicarse sin reiniciar.

### ADR-038 — Modo Inmortal / No Fail y atajos de testeo V, P, B
- **Estado:** Aceptada (código escrito; **sin compilar ni regenerar escenas**: el MCP de Unity figuraba revocado al intentarlo) · **Fecha:** 2026-11-16.
- **Lógica:** `HealthSystem.ImmortalMode`: los fallos (Miss y Bad) se registran, restan vida y rompen el combo igual que siempre, pero la vida no baja de 1 HP, así que nunca se emite `Depleted` ni hay Game Over. `RhythmSession` copia `GameSettings.ImmortalMode` a su `HealthSystem` al iniciar y al recibir `ImmortalModeChanged` (cambio en vivo, también a mitad de canción).
- **Ajuste y persistencia:** `GameSettings.ImmortalMode` (defecto `false`) + evento; `ProgressData.immortalMode`; `GameSession.SetImmortalMode` lo aplica y guarda, y `GetProgress` lo aplica al cargar. Igual que el esquema de teclas, no crea `progress.json` si no existe (evita que JUGAR se saltee la calibración).
- **Opciones (MainMenu y Pausa):** toggle "Modo Inmortal (No perder vida)" en la pestaña Accesibilidad, con una línea de ayuda; se guarda al instante.
- **HUD:** rótulo "MODO INMORTAL" (negro, 26 pt) centrado sobre la barra de vida, visible solo con el modo activo (`RhythmHudPresenter._immortalIndicator`, por evento, sin polling). Va dentro de la barra para no pisar el panel Punk, el puntaje ni el progreso.
- **Atajos (`RhythmDebugTools`, mismas `InputAction` por código, sin polling):** `V` alterna el Modo Inmortal; `P` además de F5 fuerza el Modo Punk al 100 % (escudo y crossfade del stem); `B` además de F6 dispara el debuff/proyectil del NPC. Ninguna de esas teclas choca con los esquemas de carriles (ASDF, DFJK, flechas). El texto de estado del HUD de debug muestra "Inmortal: ON/OFF [V]" y las teclas nuevas; la ayuda de Opciones > Controles también.
- **Decisión a validar:** `V` cambia solo el estado de la sesión y **no se guarda**: la preferencia persistente es la del toggle de Opciones. Así un atajo de testeo no deja el modo activado en el siguiente arranque; el indicador del HUD evita confusiones. Un resultado jugado en Inmortal igual registra récord (no se bloquea): si se quiere excluirlo del ranking, hay que decidirlo.
- **Pendiente:** compilar, regenerar con `DiosPunk/Scenes/Build MainMenu + Gameplay` y probar: con vida baja y Inmortal activo no debe aparecer Game Over; V debe mostrar/ocultar el indicador al instante; P y B deben funcionar con el esquema de teclas que sea.

### ADR-039 — Curva de niveles 3–5 más suave y calibración guiada con ajuste fino
- **Estado:** Aceptada (generador ejecutado sin errores; **no probada jugando**) · **Fecha:** 2026-11-16 · Modifica ADR-027 y ADR-036.
- **Velocidades:** 0,78 / 0,82 / 0,86 / 0,92 / 0,98 (antes 0,80 / 0,85 / 0,92 / 1,00 / 1,08).
- **Densidad (generador de charts en `RhythmLevelContentBuilder`):** nivel 3 pasa de un contratiempo cada 4 beats a uno cada 8, con los primeros 12 beats a media densidad; nivel 4, de uno cada 2 a uno cada 4; nivel 5, de uno por beat a uno cada 3. Los niveles 1 y 2 no cambian salvo la velocidad. Ningún nivel tiene notas simultáneas (medido sobre los `SongData` generados).

| Nivel | Notas | Notas/s | Separación mínima |
|---|---|---|---|
| 1 | 42 | 1,27 | 0,600 s |
| 2 | 48 | 1,40 | 0,545 s |
| 3 | 73 | 2,06 | 0,250 s |
| 4 | 100 | 2,74 | 0,231 s |
| 5 | 128 | 3,14 | 0,214 s |

- **Calibración, indicador rítmico:** un marcador recorre una pista y toca la línea de impacto roja justo con cada clic del metrónomo a 100 BPM (posición = fracción del beat calculada con `dspTime`, la misma base que programa el audio), más el pulso en la línea. Sin allocations en `Update`.
- **Calibración, entrada:** acepta cualquier tecla de carril del esquema elegido (`LaneInputSet`) o la Barra Espaciadora (antes cualquier tecla). Los 2 primeros golpes son de adaptación y no cuentan; después se toman 8 válidos (rango configurable 6–8), con el último desfase a la vista en el texto de progreso. Si tras recortar outliers quedan menos de 6 golpes consistentes, el resultado se marca no confiable, se avisa y se desactiva ACEPTAR hasta repetir o ajustar a mano.
- **Ajuste fino:** botones −5 / +5 ms con el valor actual y "Restaurar +25 ms" en el panel de resultado de la calibración (afectan al valor antes de aceptar) y en Opciones > Audio, en MainMenu y en Pausa. `GameSettings.LatencyOffsetMs` pasa a propiedad con evento (`LatencyOffsetMsChanged`): `SongClock` lo toma en vivo, así que el cambio vale incluso desde Pausa. `GameSession.SetLatencyOffset` lo guarda en `progress.json` (marca `calibrated`) sin crear el archivo si no existe.
- **Limitación conocida:** el marcador visual sale de `dspTime`, no compensa la latencia de salida de audio ni la de pantalla; quien se guía por la vista o por el oído obtiene valores algo distintos. El ajuste fino y el "Restaurar +25 ms" cubren ese caso.
- **Verificado:** el generador corrió sin errores en consola, los `LevelData` y las escenas tienen los valores y referencias nuevas, y los 5 charts cumplen la tabla. **Pendiente en vivo:** sentir la curva jugando sin Modo Inmortal, ver el marcador sincronizado con el clic y probar las teclas de calibración con cada esquema.

### ADR-040 — Chart real del Nivel 1, música definitiva, fin de canción por `clip.length` y SFX definitivos
- **Estado:** Aceptada (generador ejecutado sin errores ni warnings; **sin probar jugando ni escuchar**) · **Fecha:** 2026-11-27 · Modifica ADR-031 y ADR-039.
- **Chart del Nivel 1 ("Volver a soñar"):** `SongChartImporter` se movió a `Scripts/Rhythm/Editor/` (ensamblado `DiosPunk.Rhythm.Editor`) y ganó `TryImportFromFile` (sin diálogos). `ConfigureRealLevel1` importa las **75 notas, 101 BPM** de `Imports/Charts/Song_01_Comienzos_chart.json` en `Song_01_Comienzos` y el generador de niveles ya no le aplica el chart de prueba. El nivel sigue sin debuffs ni Modo Punk (flags de ADR-036) y velocidad ×0,78. Medido: primera nota 2,421 s, última 78,461 s.
- **Audio del nivel:** `Nivel 1 MUESTRA.mp3` (83,23 s) es el `audioClip`, importado como PCM estéreo descomprimido al cargar (timing exacto con `PlayScheduled`). `#41` (backintrack) y `#42` (línea principal) se asignan a dos campos nuevos de `SongData` (`BackingTrackClip`, `LeadLineClip`), importados comprimidos y **sin precarga** para no sumar memoria al cargar el SongData. **Todavía no se reproducen:** la doble pista del Modo Punk (ADR-032) sigue siendo una mezcla distinta y el nivel 1 no tiene `punkStem`. `ValidateLevels` exige stem solo en niveles con Modo Punk y comprueba que ninguna nota caiga después del final del audio. Los placeholders `Level1_100BPM*.wav` quedaron sin uso.
- **Fin de canción:** `RhythmTrackController.DurationSeconds` es ahora el mayor entre la última nota (+0,1 s) y `AudioClip.length`, y `SongFinished` exige notas resueltas **y** `SongTime >= DurationSeconds`. En el nivel 1 el nivel termina a los 83,23 s (cola de 4,77 s tras la última nota) y la barra de progreso llega al 100 %. Los niveles con audio placeholder terminan ~1,4 s después (cola del WAV).
- **Música de menús:** nuevo `MusicLoop` (AudioSource en loop creado en `Awake`). `Mx_MenuPrincipal_35.wav` (20 MB, es un `.wav`) suena en el menú en **streaming** (Vorbis) y se pausa en la calibración para no tapar el metrónomo; `#36` (pre-nivel) está soportado en la escena Gameplay pero **el archivo no está**, así que no suena.
- **SFX definitivos:** `SfxLibrary` suma 10 efectos (`Splash`, `UiImportant`, `PageTurn`, `Pen`, `LevelStart`, `ResultExcellent`, `ResultGood`, `CrowdShout`, `Cheer`, `Motto`). `RhythmDefinitiveAudioBuilder` mapea por rangos de la guía los `Fx_*_NN` presentes en `Imports/Audio` en cada corrida (no pisa clips asignados a mano fuera de placeholders e Imports): 0–5 gritos, 6–9 festejos, 10–16 arenga de Modo Punk, 17–30 lemas, 31/32 resultados, 33 inicio de nivel, 34 arranque, 38 clic, 39 hover, 43 opción importante, 44–48 birome, 49–52 cambio de hoja. `SoundEffectManager` ya **no repite la misma toma dos veces seguidas** cuando hay varias.
- **Asignado hoy:** `#02` gritos, `#07` festejos, `#12` Modo Punk, `#17` lemas, `#32` resultados, `#33` inicio de nivel, `#43` opción importante, `#44` birome, `#51` cambio de hoja. **Sin archivo todavía** (conservan placeholder o quedan en silencio): `#00 #01 #03–06 #08–11 #13–16 #18–31 #34 #38 #39 #45–48 #49 #50 #52`, y `#36` música pre-nivel.
- **Triggers:** `UiSfxBinder` marca como "importantes" (clic `#43`) Jugar, Partida nueva, Empezar calibración, Sí, Comenzar nivel/Finalizar y Aceptar; hover y clic comunes en el resto. `PreLevelDialoguePresenter` suena birome cada 3 letras reveladas y cambio de hoja al avanzar. `ScreenManager` suena cambio de hoja entre pantallas (no en el estado inicial ni dentro del juego) y `SceneLoader` al iniciar la cortinilla. `GameplayFlowController`: inicio de nivel (`#33`) al empezar la canción y resultados `#31` (precisión ≥ 85 %) o `#32`. `GameplaySfxBridge`: grito cada 25 aciertos seguidos, festejo cada 50 y al absorber un debuff, y lema en el Game Over.
- **Decisión a validar:** los lemas (`#17`–`#30`) suenan en el Game Over porque el único archivo presente se llama `Fx_Vidacero_17`; es fácil moverlos a otro momento en `GameplaySfxBridge`.
- **Pendiente en vivo:** escuchar los niveles y menús; comprobar que las notas coinciden con el audio (los mp3 pueden traer retardo de codificación: se corrige con `globalOffsetMs` en el JSON y reimportando); confirmar que el nivel 1 termina a los 83,2 s.
- **Fuera de este paso:** la carpeta de arte importada (`Imports/Art`) no se integró.

### ADR-041 — Pulido del Paso 1: sincronía del chart, fade de música, birome y respaldos de SFX
- **Estado:** Aceptada (código y JSON escritos; **sin compilar, sin regenerar y sin probar**: el MCP de Unity figuraba revocado al intentarlo) · **Fecha:** 2026-11-27 · Modifica ADR-040.
- **Sincronía del Nivel 1:** `globalOffsetMs` pasa de 0 a **−55** en `Imports/Charts/Song_01_Comienzos_chart.json` (negativo = las notas caen antes) para compensar el retardo del MP3 y el adelanto de la guitarra. El valor se aplica al importar: `targetTime = firstBeatSeconds + beat·60/bpm + globalOffsetMs/1000`. **Dónde tocar el ajuste fino:** ese mismo campo, de a ±10 ms (p. ej. −45 o −65), y reimportar con `DiosPunk/Scenes/Build MainMenu + Gameplay` o con el menú contextual del `SongData` > "Importar chart JSON...". El JSON lleva una nota `_ajusteSincronia` que lo explica. Es independiente de la latencia del jugador (+25 ms por defecto, ADR-035).
- **Fade de música (`MusicLoop`):** `Play`, `Pause`, `Resume` y `Stop` hacen una rampa de volumen de 0,4 s (`_fadeSeconds`, configurable). `Pause` y `Stop` detienen el audio recién cuando el fade llega a 0; `Play` tras una pausa retoma desde donde quedó. La rampa corre en `Update` con tiempo sin escalar y solo mientras hay un fade en curso (`MoveTowards` sobre un float, cero garbage). Entrar a la calibración baja la música suavemente y al salir sube; al elegir un nivel se hace fade-out mientras la cortinilla cubre la pantalla.
- **Birome:** el presentador la dispara cada 3 caracteres visibles con `_penVolume = 0,33` (multiplica al de la biblioteca, que ahora es 1) y pitch aleatorio ±0,05 (entrada `Pen` de `SfxLibrary`). Se corta de inmediato al completar la línea con un clic, al **Continuar / Saltar / elegir respuesta** (`Finish`) y al ocultarse el panel (`OnDisable`). Para eso `SoundEffectManager.Stop(SfxId)` detiene todas las voces (incluidas las que esperan un `PlayDelayed`) que reprodujeron ese efecto; el manager guarda el efecto de cada voz en un array preasignado.
- **Respaldos mientras faltan archivos propios** (`RhythmDefinitiveAudioBuilder.s_fallbacks`; se aplican solo si el efecto no tiene archivo en `Imports/Audio` y se reemplazan solos cuando llegue): hover `#39` ← `Fx_birome_44` a 0,25 con pitch 1,25–1,35 (agudo y suave, distinto del tipeo); clic `#38` ← `Fx_Start_43` a 0,45 con pitch 1,2–1,3 (suavizado, más bajo que la opción importante); arranque `#34` ← `Fx_Inicio nivel_33`; resultados excelentes `#31` ← `Fx_Arriba_07`. **Decisión mía:** para el clic usé `#43` y no el cambio de hoja `#51`, porque `#51` ya suena en cada transición de pantalla y se duplicaría en los botones de navegación.
- **Pendiente:** compilar, regenerar y escuchar: que las notas del nivel 1 caigan clavadas con el audio, que el fade se perciba suave, que la birome no sature ni quede sonando tras Continuar/Saltar, y que hover y clic de los menús no resulten molestos.

### ADR-042 — Cuenta de inicio sincronizada con la canción, birome más baja y SFX de acierto solo con metrónomo
- **Estado:** Aceptada (compilado y regenerado sin errores ni warnings propios; **sin escuchar ni probar jugando**) · **Fecha:** 2026-11-27 · Modifica ADR-040 y ADR-041.
- **Birome:** `_penVolume` baja de 0,33 a **0,14** y suena cada **4** caracteres visibles (antes 3); el corte inmediato con `SoundEffectManager.Stop(SfxId.Pen)` al completar la línea, Continuar, Saltar, responder u ocultar el panel queda igual. Valores en `PreLevelDialoguePresenter` (`_penVolume`, `_charsPerPenSound`).
- **Cuenta "1, 2, 3, ¡VA!" (#33):** el clip no se dispara más al pulsar COMENZAR. `GameplaySfxBridge` lo programa al iniciar la canción con `SoundEffectManager.PlayAtCue`, que usa `PlayScheduled` sobre `dspTime` para que el **cue del clip** caiga en un instante exacto de la canción. Medí el WAV (1,75 s): números en ~0,07 / 0,45 / 0,87 s y "¡VA!" en **~1,22 s**, valor guardado en `SfxEntry.cueSeconds` de `LevelStart` (se ajusta ahí, o en `DefinitiveSfx.cue` del generador, si llega otra toma). El "¡VA!" cae `_countInBeats` = 2 beats antes de la primera nota, sobre el grid de beats del chart (2 beats a 101 BPM = 1,19 s). La voz se corta si se pausa o se detiene la canción.
- **Sin tiempos muertos:** `SongData` gana `_leadInSeconds` (segundos entre Play y el primer sample; `SongClock.Play(clip, stem, leadIn)`). Nivel 1 usa **0,4 s** (los demás conservan 2 s): la voz arranca a 0,36 s, el "¡VA!" suena a 1,58 s y la primera nota llega a la barra a **2,77 s** (antes 4,37 s), a 1,19 s del "¡VA!". Los números 1-2-3 no caen exactamente en beats (el clip va a ~0,4 s por palabra y el pulso es de 0,59 s): se sincronizó el "¡VA!" y la nota, que es lo audible.
- **SFX sintéticos de acierto:** `SongData._useHitPlaceholderSfx` (por defecto `true`; **`false` en el Nivel 1 real**, `true` en los niveles 2 a 5 con metrónomo). `RhythmTrackController.UseHitPlaceholderSfx` lo expone y `GameplaySfxBridge` no dispara el platillo/maraca de Perfect/Good cuando es `false`. Siguen sonando el sonido de error (Miss/Bad), los de debuff y escudo, y los reales (inicio de nivel, arengas, resultados). **Decisión mía:** dejé el sonido de error activo porque es feedback y no percusión; si también choca con la mezcla, se condiciona igual.
- **Verificado:** el generador corrió sin errores; en los assets el nivel 1 tiene `leadIn = 0,4`, `hitSfx = false`, primera nota a 2,366 s y la cuenta calculada arriba; el nivel 2 conserva `leadIn = 2` y `hitSfx = true`; la escena Gameplay serializa `_penVolume = 0,14` y `_charsPerPenSound = 4`. Esta corrida también aplicó el offset −55 ms y los respaldos de SFX del ADR-041 (log: `UiHover ← #44, UiClick ← #43, Splash ← #33, ResultExcellent ← #7`).
- **Pendiente en vivo:** que el "¡VA!" se sienta en el pulso, que la primera nota llegue ~1 s después y que ya no choquen los aciertos con la banda; ajustar `_countInBeats` (p. ej. 1,5 o 2,5) o `_leadInSeconds` si hace falta.

### ADR-043 — Fix: la birome seguía sonando al saltar o comenzar
- **Estado:** Aceptada (compilado y regenerado sin errores ni warnings propios; **sin escuchar ni probar jugando**) · **Fecha:** 2026-11-27 · Modifica ADR-040 y ADR-041.
- **Diagnóstico:** el código ya usaba `AudioSource.clip` + `Play()` (nunca `PlayOneShot`) y el tipeo corre en `Update` (no en corrutina), así que `Stop(SfxId.Pen)` ya cortaba los trazos de birome. Dos hallazgos explican el síntoma: (1) el clip `Fx_birome_44` dura **2,16 s** y se reutilizaba como **hover de UI** (respaldo de `#39`, ADR-041): ese sonido está etiquetado `UiHover`, no `Pen`, así que `Stop(Pen)` no lo tocaba y la cola seguía sonando sobre la pantalla siguiente; (2) cada trazo de tipeo reproducía el clip completo y se apilaban hasta saturar las 12 voces del pool.
- **Topes de duración:** `SfxEntry.maxSeconds` (0 = clip completo). Se programa inicio y fin sobre `dspTime` (`PlayScheduled` + `SetScheduledEndTime`), sin Update ni corrutinas. Valores: trazo de birome 0,25 s, hover (respaldo con `#44`) 0,15 s y clic (respaldo con `#43`) 0,35 s; los clips definitivos propios (`#38`, `#39`) quedan en 0.
- **Canal dedicado (`SoundEffectManager`):** 3 `AudioSource` reservados fuera del pool para `SfxId.Pen`, usados en round-robin con `Stop()` + `clip` + `Play`/`PlayScheduled` (no `PlayOneShot`). Un trazo nuevo reemplaza al anterior del mismo canal (no se apilan colas) y `Stop(SfxId.Pen)` detiene los 3 de forma real e instantánea. El resto de los efectos sigue en el pool, donde `Stop(id)` corta todas las voces etiquetadas con ese id.
- **Ciclo de vida (`PreLevelDialoguePresenter`):** el flag `_isClosing` se pone en `true` en `CloseTyping()` **antes** de cualquier cambio de estado (Saltar, Continuar, elegir respuesta, `OnDisable`, `OnDestroy`) y `Update` no dispara más trazos si está activo; vuelve a `false` en `BeginDialogue`. `CloseTyping` también corta `UiHover`. El tipeo no usa corrutinas: el equivalente del chequeo "después del `yield return`" es el chequeo del flag al inicio de `Update`, que se ejecuta antes de cualquier trazo.
- **Salida de la narrativa:** `GameplayFlowController.StopNarrativeSounds()` corta birome y hover al terminar el diálogo (paso a Preparación) y al empezar la canción.
- **Verificado:** la `SfxLibrary` regenerada tiene `Pen` 0,25 s, `UiHover` 0,15 s y `UiClick` 0,35 s. **Pendiente en vivo:** saltar el diálogo, avanzar rápido y apretar Comenzar, y pasar el mouse por los botones sin que quede nada sonando.

### ADR-044 — Arte definitivo: menú principal, pausa y escenario del Nivel 1 (pista en perspectiva)
- **Estado:** Aceptada (compilado y regenerado sin errores ni warnings; verificado con renders de las escenas en el editor; **sin jugar**) · **Fecha:** 2026-11-27.
- **Importación (`ConfigureArtImports`, parte de `DiosPunk/Scenes/Build MainMenu + Gameplay`):** todos los PNG de `Imports/Art` (salvo "Guías de proporciones" y "MENU_JUGAR (PLACEHOLDERS)", que no son assets del juego) quedan como Sprite (2D and UI), Single, filtro Bilinear, sin mipmaps, Wrap Clamp, Mesh Type **Full Rect**, PPU 100, máximo 2048; las texturas de 1024 px o más (fondos, ilustración, logo, pista) usan compresión HQ y las chicas (botones, notas, receptáculos) van sin comprimir para no ensuciar los trazos. 38 sprites ajustados.
- **Menú principal (`BuildMainMenuWithArt`):** composición del boceto `MenuPrincipal_Thumbnail`: `Background`, `DiosPunk_Ilustracion` (abajo a la derecha), banner `Título_Logo` (arriba, ancho completo) y los 4 botones de papel a la izquierda en las posiciones del boceto, a tamaño nativo y con Raycast Target. El cable `Hover_Selección` marca la opción enfocada: `SelectionMarker` + `SelectionMarkerTarget` lo mueven por eventos del EventSystem (puntero o teclado), sin `Update` ni garbage. **Decisión:** "PARTIDA NUEVA" no tiene arte, así que quedó como enlace de texto blanco abajo a la derecha sobre la ilustración; el texto dibujado "JUGAR" sustituye a la etiqueta dinámica (la etiqueta de código `_playLabel` sigue referenciada pero oculta, así que ya no cambia a CONTINUAR). Los paneles de confirmación, selector de niveles, opciones, créditos y calibración conservan su look placeholder.
- **Pausa (`BuildPauseWithArt`):** hoja `Pausa_Papel` (×1,2; "PAUSA" y la calavera ya vienen dibujados) con los botones **rotados** (×1,6 para que el texto a máquina se lea) y `Selección_Marca+Flecha` como subrayado del botón enfocado (`SelectionMarker` en modo BelowTarget). Elección confirmada con el equipo.
- **Escenario del Nivel 1 (`StageTheme` + `StageThemeApplier`):** el arte se guarda en `Resources/Stages/Stage_01_Peatonal.asset`; `LevelData._stageThemeResource` lo referencia **por nombre** (como la canción) para que el menú no cargue los sprites, y `GameplayFlowController` lo aplica al cargar el nivel. Un nivel sin tema (2 a 5) conserva los visuales placeholder: el applier no hace nada. Aplica fondo, pista, receptáculos y pads con arte, rótulos de teclas en blanco sobre la base negra, panel de puntaje con `Puntaje+combo` (textos autoajustados dentro de la zona blanca) y los popups de juicio.
- **Perspectiva (decisión confirmada con el equipo):** `Highway.png` converge ~5× hacia arriba. Medí sobre el dibujo los centros de carril a varias alturas y ajusté rectas x(y): las notas caen en línea recta de `LaneBottomX` (línea de impacto, y = 590 px del dibujo) a `LaneTopX` (extremo lejano, y = 45 px), se achican hasta `noteTopScale` = 0,22 y usan los sprites `Nota_*` por carril (`NoteView.SetArt/SetPlacement`, `RhythmTrackController.ApplyStage`). Escala 1,38 (1 px = 0,0138 u); `spawnY` = 4,02; la velocidad base baja a 6,33 u/s para conservar el tiempo de anticipación (1,19 s) con una pista más corta. **Es solo visual:** el juicio sigue dependiendo de tiempos. Los colores de nota vienen del arte (sin tinte); las notas de debuff se oscurecen.
- **Popups de juicio (`JudgementPopup`):** `Perfect_1/2`, `Good_1/2`, `Miss_1/2` se alternan con un pop y un fundido de 0,45 s; el juicio Bad usa el arte de Miss. Solo activos en niveles con tema; el texto de debug `JudgementText` sigue disponible.
- **Aviso:** en el Nivel 1 el extremo lejano de la pista queda justo detrás del panel de progreso (arriba al centro): las notas nacen debajo de él, pequeñas. Si molesta, se baja `spawnY` o se mueve el panel.
- **Sin integrar (dudas de ubicación):** `Menu_Principal/Animación_ux.mp4` (video de referencia de UX, 11 MB) y la subcarpeta `MENU_JUGAR (PLACEHOLDERS)` (arte placeholder del selector de niveles); las `Selección_Marca` y `Selección_Flecha` sueltas de la pausa. El nombre de escenario del nivel 1 ("El cuarto") no coincide con el arte (la peatonal): quedó sin cambiar.
- **Verificado:** `Stage_01_Peatonal` y `LevelData` del nivel 1 con el tema; niveles 2 a 5 sin tema; renders del menú, la pausa y la pista con notas de muestra en perspectiva (alineadas con los carriles dibujados). **Pendiente en vivo:** legibilidad de las notas a velocidad real, el cable del menú al pasar el mouse, la pausa sobre la pista y el popup de juicio.

### ADR-045 — Pulido del arte: cable de selección, entrada en cascada del menú, NPC ocultos, panel de debug con F1 y rótulos de teclas
- **Estado:** Aceptada (compilado y regenerado sin errores ni warnings propios; verificado con eventos simulados y renders en el editor; **sin jugar**) · **Fecha:** 2026-11-27 · Modifica ADR-044.
- **1. Causa real del cable que no seguía (y de los sonidos de UI):** `SelectionMarkerTarget` estaba declarada en el mismo archivo que `SelectionMarker` (y `UiSfxBinder` en el de `UiSfxTrigger`). Unity solo puede guardar en una escena el componente cuya clase se llama como su archivo: las otras se serializaban con una referencia de script local rota (`m_Script: {fileID: N}` sin GUID), es decir, **"Missing Script" al jugar**. Resultado: los 5 targets del menú (y los 4 de la pausa) no hacían nada, el cable quedaba en JUGAR, y el `UiSfxBinder` de ambas escenas tampoco existía en una ejecución real (sin hover ni clic de UI propios del binder). Se separó una clase por archivo (`SelectionMarkerTarget.cs`, `UiSfxBinder.cs`, `UiSfxTrigger.cs`; se eliminó `UiSfx.cs`) y se verificó que ninguna escena conserva referencias locales. **Regla:** un MonoBehaviour por archivo, con el nombre de la clase.
- **Cable `SelectionMarker`:** ahora calcula la posición en espacio de mundo a partir de los bordes del botón (funciona con cualquier anclaje, p. ej. "Partida nueva" anclado abajo a la derecha) y sigue con un movimiento suave de 0,1 s (`Update` solo mientras se mueve, sin garbage). `OnEnable` marca **y selecciona** el botón por defecto (el teclado funciona desde el inicio) y el puntero también da el foco, así ratón y teclado comparten estado. Cubre JUGAR, OPCIONES, CRÉDITOS, SALIR y PARTIDA NUEVA (verificado: 5 targets, cable en 5 posiciones distintas).
- **2. Entrada en cascada (`MenuIntroAnimator`):** orden pedido: fondo (0 s) → ilustración (0,2 s, entra desde la derecha) → logo (0,5 s, desde arriba) → botones JUGAR, OPCIONES, CRÉDITOS, SALIR y PARTIDA NUEVA cada 0,1 s desde las 0,8 s (desde la izquierda) → cable (1,4 s). Interpola `CanvasGroup.alpha`, `anchoredPosition` y escala con ease-out cúbico y tiempo sin escalar; ~1,65 s en total, arrays creados una sola vez, `Update` solo mientras anima. Bloquea los clics hasta terminar, se reproduce una vez por carga de escena (volver de Opciones o Créditos no la repite) y al terminar reubica el cable. **Nota:** no pude abrir `Animación_ux.mp4` (video); los tiempos y direcciones de entrada son una interpretación del orden indicado, para ajustar mirando el video.
- **3. NPC ocultos sin debuffs:** `RhythmSession.LevelConfigured` avisa las mecánicas del nivel y `NpcSabotageController` desactiva los NPC laterales cuando `enableDebuffs == false` (Nivel 1). Un debuff forzado por debug (F6 / B) igual lanza el proyectil, pero sin los personajes.
- **4. Panel de debug con F1:** `RhythmDebugTools` arranca con el panel **oculto** (estadísticas, métricas, GC y el bloque de estado con la lista de atajos) y **F1 lo muestra u oculta**; los atajos funcionan aunque esté oculto. **Decisión de diseño:** F1 dejó de alternar el Auto-Play (ahora es el panel); el Auto-Play pasó a **F7** y F4 sigue cambiando el modo del bot. El panel lista: F1 panel, F2 Reiniciar, F3 Pausar, F4 Modo Bot, F5/P Modo Punk, F6/B Debuff, V Inmortal, F7 Auto-Play, +/- Latencia (la ayuda de Opciones > Controles también). En niveles con arte, el texto de juicio con milisegundos (`JudgementText`) se oculta porque lo reemplazan los popups.
- **5. Rótulos de teclas en el Nivel 1:** con tema, los `LaneKeyLabels` quedan **centrados sobre cada receptáculo** (antes debajo, sobre la base negra), en blanco, negrita, tamaño 6 y contorno negro, dibujados sobre el receptáculo y el pad pero debajo de las notas (que los cubren al llegar). Siguen actualizándose solos según el esquema de Opciones (ASDF, DFJK, Flechas o Todos).
- **Nota de diseño — ubicación provisional de "PARTIDA NUEVA":** no tiene arte propio, así que es un enlace de texto blanco (36 pt, una sola línea) abajo a la derecha sobre la ilustración, marcado también por el cable. Es **provisional**: cuando Arte dibuje su botón, se lo agrega a la columna de papeles de la izquierda (el builder `BuildMainMenuWithArt` y la entrada en cascada ya lo tratan como un botón más).
- **Pendiente en vivo:** que el cable siga con mouse y flechas, el ritmo de la cascada, los rótulos sobre los receptáculos con notas reales, F1 mostrando y ocultando el panel y los sonidos de UI (hover y clic) ahora que el binder existe.

<!-- Añadir ADR-046+ debajo -->

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
| SFX (audio) | `SoundEffectManager`, `SfxLibrary`, `GameplaySfxBridge`, `UiSfxBinder`, slider SFX | 🟨 Compila y el generador valida la biblioteca; placeholders procedurales sin escuchar; faltan los clips definitivos del área de audio |
| Doble pista Modo Punk | `SongClock` con stem sincronizado y crossfade, `SongData._punkStemClip` | 🟨 Stems alineados a la muestra (validado en el generador); crossfade sin ver/escuchar en vivo; faltan los stems definitivos |
| Cortinilla de carga | `SceneTransitionOverlay`, `SceneLoader` | 🟨 Prefab renderizado y fundido iniciado en Play Mode; transición completa sin verificar en vivo |
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
- 2026-11-27 · [Arte/UX/Fix] · Pulido del arte (ADR-045): fix de scripts faltantes en escenas (una clase por archivo: `SelectionMarkerTarget`, `UiSfxBinder`, `UiSfxTrigger`), `SelectionMarker` en espacio de mundo con selección inicial, `MenuIntroAnimator` (cascada), NPC ocultos sin debuffs (`RhythmSession.LevelConfigured`), panel de debug oculto con F1 (Auto-Play a F7), rótulos de teclas sobre los receptáculos del Nivel 1. Compilado y regenerado.
- 2026-11-27 · [Arte] · Paso 2, arte definitivo (ADR-044): `RhythmArtBuilder` (Texture Importer de Imports/Art, `StageTheme` del nivel 1 con geometría medida, menú y pausa con arte), `StageTheme`, `StageThemeApplier`, `JudgementPopup`, `SelectionMarker`/`SelectionMarkerTarget`, perspectiva en `RhythmTrackController.PlaceNote` y `NoteView.SetArt/SetPlacement`, `LevelData._stageThemeResource`. Compilado y regenerado.
- 2026-11-27 · [Audio/Fix] · Birome que seguía sonando (ADR-043): canal dedicado de 3 `AudioSource` para `Pen`, `SfxEntry.maxSeconds` (birome 0,25 s, hover 0,15 s, clic 0,35 s) con `SetScheduledEndTime`, flag `_isClosing` y `CloseTyping()` en `PreLevelDialoguePresenter`, `StopNarrativeSounds()` en `GameplayFlowController`. Compilado y regenerado.
- 2026-11-27 · [Audio/Gameplay] · Cuenta de inicio y SFX (ADR-042): cuenta "1, 2, 3, ¡VA!" programada con `PlayAtCue` (`SfxEntry.cueSeconds`) 2 beats antes de la primera nota, `SongData._leadInSeconds` (nivel 1 = 0,4 s) y `_useHitPlaceholderSfx` (falso en el nivel 1), birome a 0,14 cada 4 caracteres, `SongClock.SongStartDsp`, `RhythmTrackController.FirstNoteTime/SongBpm/UseHitPlaceholderSfx`. Compilado y regenerado.
- 2026-11-27 · [Audio/Pulido] · Pulido del Paso 1 (ADR-041): `globalOffsetMs = -55` en el chart del nivel 1, fade de 0,4 s en `MusicLoop`, birome a 0,33 con corte en Continuar/Saltar (`SoundEffectManager.Stop(SfxId)`), respaldos de hover, clic, arranque y resultados excelentes con audio ya disponible. **Sin compilar ni regenerar** (MCP revocado).
- 2026-11-27 · [Audio/Charts] · Paso 1 de audio definitivo (ADR-040): chart real del nivel 1 (75 notas, 101 BPM) vía `SongChartImporter` (movido a `Editor/`), `Nivel 1 MUESTRA.mp3` + capas #41/#42 en `SongData`, `MusicLoop` con música de menú #35 (y soporte #36), fin de nivel por `clip.length` (83,2 s), 10 SFX nuevos en `SfxLibrary` mapeados por rangos de la guía (`RhythmDefinitiveAudioBuilder`), toma sin repetición en `SoundEffectManager`, triggers de birome, cambio de hoja, inicio de nivel, resultados y arengas. Se eliminó el campo sin uso `RhythmDebuffVisuals._messageSeconds` (consola en 0 warnings).
- 2026-11-16 · [Balance/Calibración] · Cierre de balance y calibración (ADR-039): velocidades 0,78–0,98, menos contratiempos en niveles 3–5, indicador rítmico en `CalibrationScreen`, 2 golpes de adaptación + 8 válidos, teclas del esquema o Barra Espaciadora, ajuste fino ±5 ms y restaurar +25 en calibración y Opciones, `GameSettings.LatencyOffsetMs` con evento, `ProgressStore.SetLatency`. Escenas y niveles regenerados (también incluyen los Bloques 2 y 3).
- 2026-11-16 · [Rhythm/Flow/Debug] · Bloque 3, Modo Inmortal y atajos (ADR-038): `HealthSystem.ImmortalMode` (piso de 1 HP), `GameSettings.ImmortalMode`/evento, `ProgressData.immortalMode`, `GameSession.SetImmortalMode`, toggle en `OptionsScreen`, indicador en `RhythmHudPresenter`, teclas V/P/B en `RhythmDebugTools`. Builders actualizados. **Sin compilar ni regenerar escenas.**
- 2026-11-16 · [Input/Flow] · Bloque 2, esquema de teclas (ADR-037): `ControlScheme`/`ControlSchemes`/`LaneInputSet` (nuevo `ControlScheme.cs`), `LaneKeyLabels`, `GameSettings.ControlScheme`, `ProgressData.controlScheme`, `GameSession.SetControlScheme`, selector en `OptionsScreen`, tutorial de `CalibrationScreen` con el esquema; `RhythmInputReader` reescrito sobre `LaneInputSet`. Builders actualizados. **Sin compilar ni regenerar escenas** (MCP revocado).
- 2026-11-16 · [Rhythm/Flow/Balance] · Bloque 1 de feedback v0.2.0 (ADR-034, ADR-035, ADR-036): ventanas 55/115 (Punk 75/145) con juicio `Bad` hasta 155/185 ms, vida Miss −10 / Bad −5 / Perfect +3 / Good +1,5, latencia por defecto +25 ms, calibración con mediana y descarte de outliers, flags de mecánicas por nivel (`LevelData`, `RhythmSession.ConfigureLevel`, `DebuffSystem.Enabled/FirstTrigger`, `PunkModeSystem.Enabled`) y curva de velocidad ×0,80 / 0,85 / 0,92 / 1,00 / 1,08. **Sin compilar ni regenerar escenas:** MCP de Unity revocado.
- 2026-11-09 · [Audio/Flow] · Infraestructura de audio y transición (ADR-031, ADR-032, ADR-033): `SoundEffectManager` (pool de 12 voces, `GameSettings.SfxVolume`), `SfxLibrary` + `SfxId`, `GameplaySfxBridge`, `UiSfxTrigger`/`UiSfxBinder`, slider "Volumen de efectos (SFX)" en `OptionsScreen`; doble pista en `SongClock` (`_punkSource`, `Play(clip, punkStem)`, `SetPunkStemActive`, crossfade) con `SongData._punkStemClip` y `RhythmSession` conectada al Modo Punk; `SceneTransitionOverlay` + `SceneLoader` con fundido y activación retenida. Builder nuevo `RhythmAudioBuilder` (11 SFX procedurales, 6 stems punk, grano de papel y prefab de la cortinilla); el generador de escenas valida los stems. Assets nuevos: `Audio/Placeholders/Sfx/*.wav`, `Audio/Placeholders/*_PunkStem.wav`, `Art/Placeholders/PaperGrain_Placeholder.png`, `Resources/Audio/SfxLibrary.asset`, `Resources/UI/SceneTransition.prefab`.
- 2026-11-09 · [Rhythm/HUD] · Feedback de absorción fuera de los carriles (ADR-030): sello compacto arriba a la izquierda (1,2 s, desvanecimiento con `CanvasGroup`), destello radial sutil y localizado bajo la HitBar, anillo de escudo más chico y bajo; sprite nuevo `ShieldGlow_Placeholder.png`; `NpcSabotageController` suma `_bannerGroup`, `_flashAlpha` y `_absorbEffectSeconds`.
- 2026-11-09 · [Rhythm/HUD] · Progreso de la pista y rótulos de NPC (ADR-028, ADR-029): `SongProgressPresenter`, `TimeFormatter`, `RhythmTrackController.DurationSeconds`; panel `SongProgressPanel` en el HUD; rótulo "PLACEHOLDER / REEMPLAZAR POR DIBUJO REAL" en ambos NPC (el derecho con `flipX`); textos de juicio y debuff reubicados.
- 2026-11-09 · [Rhythm/Balance] · Ajustes tras pruebas reales (ADR-024..027): carga continua del Modo Punk (+1 % Perfect, +0,5 % Good, +15 % Miss), "tomatazo" y cartel "¡DEBUFF ABSORBIDO!" en `NpcSabotageController`, notas +30 % y carriles a 1,9 u, nivel 2 más suave (`LevelData._noteSpeedMultiplier`, `SongData._testWarmupBeats`). Sprite nuevo `TomatoSplat_Placeholder.png`; builder genera splat, cartel y destello. Sin cambios en `progress.json`.
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
