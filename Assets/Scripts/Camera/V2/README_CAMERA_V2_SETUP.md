# MiniMarket Camera V2 Setup

## Objetos na Hierarchy

Crie ou mantenha estes objetos:

```text
CameraSystemV2
ThirdPersonCAM
FirstPersonCAM
Character 01
└── POV
```

## CameraSystemV2

Componentes:

```text
CameraV2Controller
CameraV2LegacyBlocker
CameraV2F10Diagnostics
```

Configuração do `CameraV2Controller`:

```text
Third Person CAM = ThirdPersonCAM
First Person CAM = FirstPersonCAM
Player Target = Character 01
POV = POV
Usar Botao Direito Para Primeira Pessoa = ligado
Botao Primeira Pessoa = 1
Voltar Para Terceira Ao Soltar = ligado
Iniciar Em Terceira Pessoa = ligado
Sincronizar Yaw Ao Trocar = ligado
Controlar Audio Listeners = ligado
```

## ThirdPersonCAM

Componentes:

```text
Camera
AudioListener
Camera3Person
```

Tag:

```text
MainCamera
```

Configuração principal:

```text
Camera 3 Person = Camera do próprio objeto
Target = Character 01
Camera Ativa = ligado
Controlar Camera Component = ligado
Travar Cursor Ao Ativar = ligado
Aceitar Input Mouse = ligado
Usar Colisao = ligado
Auto Alinhar Atras Do Personagem = desligado inicialmente
```

## FirstPersonCAM

Componentes:

```text
Camera
AudioListener
Camera1Person
GetItemV2
```

Tag:

```text
Untagged
```

Configuração principal:

```text
Camera 1 Person = Camera do próprio objeto
Corpo Personagem = Character 01
Ponto POV = POV
Get Item = GetItemV2 do próprio FirstPersonCAM
Camera Ativa = desligado inicialmente
Ativar Get Item Junto = ligado
```

## POV

Crie um Empty dentro de `Character 01`:

```text
Local Position = X 0 / Y 1.68 / Z 0.18
Local Rotation = X 0 / Y 0 / Z 0
Local Scale = X 1 / Y 1 / Z 1
```

Não coloque Camera, AudioListener, Collider, Rigidbody ou Script no POV.

## Objetos pegáveis

Em cada objeto/produto pegável, coloque:

```text
GetItemObjectV2
Collider
Rigidbody
```

Rigidbody recomendado:

```text
Use Gravity = ligado
Is Kinematic = desligado
Collision Detection = Continuous Dynamic
Interpolation = Interpolate
```

## Diagnóstico F10

O script `CameraV2F10Diagnostics` mostra um painel de diagnóstico ao apertar F10.

Ele pode ser colocado no `CameraSystemV2`, mas também é criado automaticamente em runtime.

Use F10 para verificar:

```text
Controller encontrado
ThirdPersonCAM encontrado
FirstPersonCAM encontrado
GetItemV2 encontrado
Câmera ativa
Yaw/Pitch
Zoom
Objeto selecionado/pegando
FPS aproximado
```

## Importante

O V2 não usa mais:

```text
CAM_GetItem.cs antigo
MiniMarketCameraDiagnosticsTuner.cs antigo
MiniMarketCameraRealtimeAnomalyLogger antigo
CameraGTAFollowHardcore antigo
```

Se aparecer erro vermelho no Console, o Unity não deixa adicionar componentes novos. Resolva primeiro o erro vermelho.
