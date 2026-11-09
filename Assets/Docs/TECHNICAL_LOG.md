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

<!-- Añadir ADR-034+ debajo -->

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
