# Relatório de Organização dos Scripts

Executado em: 2026-07-11 17:03:29

- Sistemas de câmera e movimentação removidos.
- Cenas e prefabs limpos.
- Prefixo `MiniMarket` removido das classes restantes.
- Scripts organizados por função.
- Brick Project Studio ignorado.

## Câmera/movimentação removidos

- `Assets/Scripts/Performance/MiniMarketLightingPerformanceOptimizer.cs`
- `Assets/Scripts/Performance/MiniMarketRuntimePerformanceOptimizer.cs`
- `Assets/Scripts/Stamina/MiniMarketSegmentedStaminaRuntimeGuard.cs`
- `Assets/Scripts/Camera/V2/Diagnostics/CameraV2F10Diagnostics.cs`
- `Assets/Scripts/Performance/MiniMarketMenuPerformanceBridge.cs`
- `Assets/Scripts/Performance/MiniMarketPlayerShutdownGuard.cs`
- `Assets/Scripts/Camera/LegacyCompatibility/CrosshairAim.cs`
- `Assets/Scripts/Camera/V2/Menu/CameraV2MenuInputBlocker.cs`
- `Assets/Scripts/Physics/MiniMarketPlayerRigidbodyPusher.cs`
- `Assets/Scripts/BuyScene/BuySceneLandPurchaseController.cs`
- `Assets/Scripts/Camera/V2/FirstPersonCAM/Camera1Person.cs`
- `Assets/Scripts/Camera/V2/ThirdPersonCAM/Camera3Person.cs`
- `Assets/Scripts/BuyScene/BuySceneCameraModeController.cs`
- `Assets/Scripts/Camera/V2/CameraV2LegacyBlocker.cs`
- `Assets/Scripts/UI/MiniMarketMiniMapController.cs`
- `Assets/Scripts/BuyScene/BuySceneEntryTrigger.cs`
- `Assets/Scripts/UI/MiniMarketEnergySegmentHUD.cs`
- `Assets/Scripts/Camera/V2/CameraV2Controller.cs`
- `Assets/Scripts/Player_StaminaHUD.cs`
- `Assets/Scripts/Player_Move.cs`

## Sem uso removidos

- Nenhum.

## Renomeados

- `MiniMarketPlayerDatabase -> PlayerDatabase`
- `MiniMarketRuntimeDiagnostics -> RuntimeDiagnostics`
- `MiniMarketSceneReferenceRepair -> SceneReferenceRepair`
- `MiniMarketUpgradeLogger -> UpgradeLogger`
- `MiniMarketMenuController -> MenuController`
- `MiniMarketPlayerProfile -> PlayerProfile`
- `MiniMarketUIButtonSpriteSwap -> UIButtonSpriteSwap`
- `MiniMarketObjectPhysicsProfile -> ObjectPhysicsProfile`

## Movidos

- `Assets/Scripts/Database/MiniMarketPlayerDatabase.cs -> Assets/Scripts/Database/PlayerDatabase.cs`
- `Assets/Scripts/Debug/MiniMarketRuntimeDiagnostics.cs -> Assets/Scripts/Debug/RuntimeDiagnostics.cs`
- `Assets/Scripts/Debug/MiniMarketSceneReferenceRepair.cs -> Assets/Scripts/Debug/SceneReferenceRepair.cs`
- `Assets/Scripts/Debug/MiniMarketUpgradeLogger.cs -> Assets/Scripts/Debug/UpgradeLogger.cs`
- `Assets/Scripts/Menu/MiniMarketMenuController.cs -> Assets/Scripts/Menu/MenuController.cs`
- `Assets/Scripts/Menu/MiniMarketPlayerProfile.cs -> Assets/Scripts/Menu/PlayerProfile.cs`
- `Assets/Scripts/Menu/MiniMarketUIButtonSpriteSwap.cs -> Assets/Scripts/Menu/UIButtonSpriteSwap.cs`
- `Assets/Scripts/Physics/MiniMarketObjectPhysicsProfile.cs -> Assets/Scripts/Physics/ObjectPhysicsProfile.cs`
- `Assets/Scripts/CicloSolar.cs -> Assets/Scripts/World/CicloDiaNoite.cs`
- `Assets/Scripts/GetItem.cs -> Assets/Scripts/Interaction/GrabbableObjectHardcore.cs`
- `Assets/Scripts/Player_Gold.cs -> Assets/Scripts/Economy/PlayerGold.cs`
- `Assets/Scripts/Player_GoldHUD.cs -> Assets/Scripts/Economy/GoldHUD.cs`
- `Assets/Scripts/BuyScene/BuyableLandAreaMarker.cs -> Assets/Scripts/Purchasing/BuyableLandAreaMarker.cs`
- `Assets/Scripts/BuyScene/BuyScenePurchaseConfirmationPanel.cs -> Assets/Scripts/UI/BuyScenePurchaseConfirmationPanel.cs`
- `Assets/Scripts/BuyScene/BuySceneUIImageButton.cs -> Assets/Scripts/UI/BuySceneUIImageButton.cs`
- `Assets/Scripts/Database/PlayerDatabase.cs -> Assets/Scripts/Data/PlayerDatabase.cs`
- `Assets/Scripts/Debug/RuntimeDiagnostics.cs -> Assets/Scripts/Diagnostics/RuntimeDiagnostics.cs`
- `Assets/Scripts/Debug/SceneReferenceRepair.cs -> Assets/Scripts/Diagnostics/SceneReferenceRepair.cs`
- `Assets/Scripts/Debug/UpgradeLogger.cs -> Assets/Scripts/Diagnostics/UpgradeLogger.cs`
- `Assets/Scripts/Menu/MenuController.cs -> Assets/Scripts/Menus/MenuController.cs`
- `Assets/Scripts/Menu/PlayerProfile.cs -> Assets/Scripts/Data/PlayerProfile.cs`
- `Assets/Scripts/Menu/UIButtonSpriteSwap.cs -> Assets/Scripts/Menus/UIButtonSpriteSwap.cs`
- `Assets/Scripts/Physics/ObjectPhysicsProfile.cs -> Assets/Scripts/Data/ObjectPhysicsProfile.cs`
- `Assets/Scripts/Interaction/V2/GetItem/GetItemObjectV2.cs -> Assets/Scripts/Interaction/GetItemObjectV2.cs`
- `Assets/Scripts/Interaction/V2/GetItem/GetItemV2.cs -> Assets/Scripts/Interaction/GetItemV2.cs`

## Cenas limpas

- `Assets/_Recovery/0.unity`
- `Assets/Scenes/SampleScene.unity`

## Prefabs limpos

- Nenhum.

## Avisos

- `Mover Assets/Scripts/BuyScene/BuySceneEntryAreaStatusSync.cs: Cannot move asset.  is not a valid path.`

## Objetos e componentes

- Objetos de câmera removidos: 2
- Componentes removidos: 13
- CharacterControllers removidos: 1
- Missing Scripts removidos: 4
