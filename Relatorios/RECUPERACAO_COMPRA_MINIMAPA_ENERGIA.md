# Recuperação de compra, minimapa, energia, diagnósticos e mira

Atualizado em: 2026-07-11

## Escopo

Este relatório registra a recuperação dos sistemas que deixaram de funcionar após a organização automática local de scripts e cena.

## Diagnóstico de causa raiz

### Compra de terrenos

O sistema de compra antigo continuava usando `BuySceneCameraModeController`, mas o novo `PlayerCameraController` também aplicava posição, rotação e FOV em `LateUpdate`. Os dois sistemas disputavam a mesma câmera. Além disso, componentes antigos da BuyScene foram removidos ou ficaram sem referências durante a organização local.

### Falha de compilação CS0246

A organização local apagou `BuySceneCameraModeController.cs` e `BuySceneEntryTrigger.cs` da pasta antiga, enquanto os novos arquivos `PurchaseModeBridge`, `PurchaseSystemBootstrapHost` e `FirstPersonReticleController` continuavam dependendo desses tipos. Isso bloqueava todo o Play Mode.

Correção aplicada:

- `BuySceneCameraModeController.cs` foi restaurado em `Assets/Scripts/Purchasing`;
- `BuySceneEntryTrigger.cs` foi restaurado em `Assets/Scripts/Purchasing`;
- os GUIDs originais foram preservados para manter referências de cenas e prefabs;
- os arquivos antigos em `Assets/Scripts/BuyScene` foram removidos para impedir classes duplicadas;
- o controlador restaurado usa `CameraRelativeMovement` e `PlayerCameraController` como referências oficiais.

### Minimapa

A câmera principal desativava todas as outras câmeras da cena. Isso incluía a câmera do minimapa, mesmo ela renderizando para uma `RenderTexture`. O minimapa antigo também procurava `PlayerMove`, que não é mais o movimento oficial.

### Diagnósticos

O painel antigo pertencia à Camera V2 e foi removido junto com os scripts obsoletos.

### Energia grátis e HUD

O HUD estava colocado em um Text e procurava imagens somente nos próprios filhos. As barras visuais estavam como objetos irmãos ou em outro nível do container. A restauração gratuita também podia ser sobrescrita por listeners antigos no mesmo frame.

### Mira

Não existia um controlador de visibilidade ligado ao modo atual da câmera.

## Arquitetura corrigida

### `PlayerCameraController`

- preserva câmeras auxiliares com `targetTexture`;
- expõe `SetExternalPoseControl(bool)`;
- suspende atualização de pose e cursor quando outro sistema assume a câmera;
- retoma imediatamente a pose normal ao liberar o controle externo.

### `BuySceneCameraModeController`

- usa a câmera oficial do `PlayerCameraController`;
- mantém os campos públicos antigos para preservar a serialização da cena;
- controla visão aérea, modo ortográfico, cursor e restauração da pose;
- não recria sistemas antigos de câmera;
- mantém compatibilidade com os marcadores de terreno existentes.

### `BuySceneEntryTrigger`

- funciona no collider real da calçada;
- detecta `CameraRelativeMovement` e `CharacterController`;
- abre e fecha com E;
- recria borda e X visual por `LineRenderer` em runtime;
- procura terrenos próximos quando a lista serializada estiver vazia;
- não cria objetos durante `OnValidate`.

### `PurchaseModeBridge`

- observa `BuySceneCameraModeController.ModoCompraAtivo`;
- bloqueia o input do gameplay;
- entrega a câmera ao modo de compra;
- restaura câmera, tag, AudioListener e input ao sair;
- possui bootstrap runtime para reparar referências, painel, triggers, terrenos e linhas.

### `RuntimeMiniMap`

- inicialização automática;
- alvo oficial: `CameraRelativeMovement`;
- câmera ortográfica para `RenderTexture`;
- UI circular e zoom;
- tecla M;
- resolução 512 no desktop e 256 no mobile.

### `RuntimeDiagnosticsPanel`

- inicialização automática;
- tecla F10;
- informações de performance, câmeras, banco, energia, compra e minimapa;
- não depende da Camera V2.

### `MiniMarketEnergySegmentHUD`

- detecta barras no container visual;
- suporta barra total e barras segmentadas;
- anima o preenchimento;
- exibe reserva de recarga no próximo segmento;
- mantém atualização por eventos e verificação lenta de segurança.

### `FreeEnergyRestoreService`

- detecta o botão de energia grátis por nome/texto;
- restaura banco e movimento imediatamente;
- reaplica no fim do frame e após pequeno atraso;
- força atualização visual do HUD.

### `FirstPersonReticleController`

- detecta elementos de mira pelo nome;
- mostra somente em primeira pessoa;
- oculta em terceira pessoa, menu e compra.

## Avisos do Input Manager e Burst

O aviso de depreciação do Input Manager não bloqueia compilação. O projeto mantém entrada legada por compatibilidade enquanto o suporte mobile externo é conectado.

As mensagens `not a known Burst entry point` vêm dos pacotes de renderização/Burst, não dos scripts de gameplay. O projeto está em Unity `6000.7.0a1` e o lock de pacotes resolve Burst builtin 2.0.0, enquanto dependências de Collections e Render Pipeline declaram versões 1.8.x. Após corrigir todos os erros C#, deve-se fechar o Unity e limpar primeiro os caches gerados de Burst/Temp. Não alterar versões de pacotes antes de confirmar se as mensagens permanecem após a recompilação limpa.

## Ferramenta de reparo da cena

Executar fora do Play Mode:

```text
Tools > Game Systems > Repair Purchase Minimap Diagnostics Energy Reticle
```

A ferramenta:

- conecta `PurchaseModeBridge`;
- conecta controlador, triggers, terrenos e painel;
- reativa demarcações;
- liga `MiniMarketEnergySegmentHUD` ao movimento;
- rebusca as imagens das barras;
- salva a cena somente quando necessário.

Ela não remove objetos, não altera GUIDs e não toca em Brick Project Studio.

## Checklist após `git pull`

1. Console com zero erros vermelhos.
2. Executar a ferramenta de reparo.
3. Salvar a cena.
4. Play Mode.
5. Aproximar-se da calçada de compra.
6. Confirmar que a marcação aparece.
7. Pressionar E e conferir a visão de compra.
8. Selecionar um terreno e abrir o painel de confirmação.
9. Pressionar M e validar o minimapa e os botões de zoom.
10. Pressionar F10 e validar o painel de diagnóstico.
11. Gastar energia e observar as barras diminuindo.
12. Parar e observar recarga visual.
13. Usar Energia Grátis e aguardar pelo menos dois segundos; a energia deve permanecer completa.
14. Conferir que a mira está oculta em terceira pessoa.
15. Entrar em primeira pessoa e conferir que a mira aparece.

## Observação sobre a área da calçada

O reparo não inventa uma posição para a entrada. Deve existir um collider no objeto real da calçada com `BuySceneEntryTrigger`. Se nenhum trigger for encontrado, o Console informa exatamente essa pendência.

## Organização de scripts

A limpeza automática destrutiva foi desativada. O organizador agora apenas gera uma auditoria em Markdown e nunca move, renomeia ou apaga arquivos automaticamente.
