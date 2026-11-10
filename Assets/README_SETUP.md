# Racing Kit — Prometeo + Dodge Challenger (Unity)

Compatible con Unity 2022 LTS y Unity 6. Todo está pensado para URP (también funciona en Built-in).
Importante: PCC usa el Input clásico → Project Settings > Player > **Active Input Handling = Both** (o Input Manager Old).

## 1. Carro
1. Descarga el modelo de Sketchfab (revisa que sea descargable y su licencia; da crédito si lo pide) en **glTF/FBX**. Si es glTF, instala el paquete *glTFast*.
2. Si las 4 ruedas NO son objetos separados, ábrelo en Blender: separa cada rueda, origen en el centro del eje, escala 1 unidad = 1 m, +Z adelante. Exporta FBX.
3. En la escena: arrastra el prefab de PCC (el carro de ejemplo), **borra su carrocería** y mete el Challenger como hijo. Asigna las 4 mallas de rueda a `Front/Rear Left/Right Mesh` de PrometeoCarController y mueve/ajusta los 4 WheelColliders al centro de cada rueda (radio ≈ 0.36).
4. Rigidbody: Mass 1550, Drag 0.05, Interpolate (CarFeel lo fuerza).
5. WheelColliders: Suspension Distance 0.25, Spring 35000, Damper 4500, Target Position 0.5; Forward friction Stiffness 1.6; Sideways friction Stiffness 2.2.
6. PrometeoCarController: maxSpeed 220, maxReverseSpeed 40, accelerationMultiplier 5, brakeForce 400, steeringSpeed 0.25, decelerationMultiplier 3, handbrakeDriftMultiplier 4, useUI = OFF (usamos RacingHUD).
7. Añade al carro: **CarFeel, Gearbox, CollisionFeedback, CarLights, CarCustomizer** (arrastra el PCC a los campos `pcc`).

> Si tu versión de PCC no tiene algún campo (`bodyMassCenter`, `isTractionLocked`, `maxSteeringAngle`), ajusta el nombre en CarFeel.cs.

## 2. Cámara
Main Camera → **CarCamera**: target = carro, targetRb = su Rigidbody. Quita cualquier script de cámara previo.

## 3. Terrain + Road
1. Crea el Terrain, esculpe y pinta como quieras. Crea una **Terrain Layer** con tu textura de asfalto.
2. Crea un empty `Road`, añade **TerrainRoadPainter**, asigna Terrain y Road Layer.
3. Coloca 10–20 empties (waypoints) en orden formando el circuito, arrástralos a `Waypoints`.
4. Click derecho en el componente → **Pintar carretera en Terrain** (Ctrl+Z lo deshace). Puedes repetir tras mover waypoints.
5. Anota el índice de la capa de asfalto en Terrain Layers (0,1,2…) → lo usa el brush de edificios.
6. Elimina árboles/detalles que hayan quedado sobre la vía y añade guardarraíles/bordillos como prefabs si quieres.

## 4. Brush de edificios
Menú **Tools > Brush de Edificios** → arrastra tus prefabs (con collider), elige Terrain, pon el índice de la capa de asfalto para que no se construya sobre la carretera, activa el pincel.
Clic = pintar, Ctrl+clic = borrar, Alt = mover la vista. Todo cae dentro de un objeto `Edificios` y se marca Static.
Alternativa nativa: Terrain > Paint Trees > Add Tree con tus edificios (menos control, pero más eficiente con miles).

## 5. Vueltas
1. Crea 6–10 empties con **Checkpoint** (BoxCollider trigger) cruzando la carretera; el eje azul (Z) debe apuntar en el sentido de la carrera. El #0 es la meta/salida.
2. Empty `Race` + **LapManager**: arrastra los checkpoints EN ORDEN, el carro (Rigidbody) y el PCC. Tecla **R** = volver al último checkpoint.

## 6. HUD (Canvas Screen Space Overlay, escala 1920×1080)
Crea capas hijas del Canvas (cada una un Panel/RectTransform vacío) y arrástralas a `Layers` de **RacingHUD**:
- `Velocimetro` (abajo derecha) → depth 1.0, rollAmount 1  — dentro: texto velocidad, texto marcha, aguja de velocidad (Image con pivot en la base), barra RPM (Image Filled)
- `Vueltas` (arriba izquierda) → depth 0.6, rollAmount 0.5 — textos vuelta/tiempo/mejor/última
- `Marco` (esquinas, decoración) → depth 0.3
- `SpeedLines` e `ImpactFlash`: Images a pantalla completa, color alpha 0, Raycast Target OFF.
- Texto central grande para la cuenta regresiva → `countdownText`.
Asigna car, gearbox, lapManager. Tuning: stiffness/damping para el "peso" del HUD, impactForce para la violencia del golpe.

## 7. Identidad visual y luces
- **CarCustomizer**: asigna los Renderers del Challenger, ajusta `paintMaterialKeywords` al nombre real de los materiales de pintura, elige paleta (negro mate + neón cian por defecto). Click derecho → Aplicar y mueve las posiciones del kit hasta que encajen con el modelo. Para cambios de geometría reales (capó, parachoques) hazlo en Blender.
- **AtmosphereController** en un empty con tu Directional Light: atardecer por defecto (timeOfDay 18.2). `dayLengthMinutes` > 0 hace ciclo día/noche.
- **CarLights**: 2 Spot Lights como faros, 2 Point Lights bajo el chasis (underglow), calaveras con material con Emission.
- Postproceso (Global Volume URP): Bloom 0.8, Vignette 0.25, Color Adjustments (contraste +10), Motion Blur leve, Tonemapping ACES. Postes de luz con Point Lights para la noche.
- Identidad: elige UNA paleta (ej. negro + cian + magenta) y úsala en carro, HUD, checkpoints y señales.
