# Validação profissional no Unity 6

Atualizado em: 2026-07-15

## Objetivo

Definir um gate repetível para impedir regressões de cena, persistência, física e arquitetura antes de integrar alterações na `main`.

O projeto está fixado em `6000.7.0a1`. Por ser uma versão alpha, não atualizar `ProjectVersion.txt`, URP ou pacotes de IA automaticamente. Mudanças de versão devem ocorrer em branch própria, com backup, reimportação completa e validação de Desktop/Mobile.

## Referências oficiais aplicadas

- [`DontDestroyOnLoad`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Object.DontDestroyOnLoad.html): preservar um componente/root também preserva seus filhos; por isso fachadas anexadas ao jogador não devem chamar essa API.
- [`Physics.ClosestPoint`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.ClosestPoint.html): suporta primitivas e `MeshCollider` convexo; os demais casos usam bounds no MiniMarket.
- [`FindAnyObjectByType`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Object.FindAnyObjectByType.html): adequado quando qualquer instância serve, mas deve ser cacheado e não executado continuamente.
- [`Undo.RecordObject`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Undo.RecordObject.html): ferramentas que alteram componentes devem permitir reversão.
- [`EditorSceneManager.MarkSceneDirty`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SceneManagement.EditorSceneManager.MarkSceneDirty.html): a ferramenta marca a cena; o salvamento permanece uma decisão explícita.
- [Unity Test Framework em linha de comando](https://docs.unity3d.com/Packages/com.unity.test-framework%401.4/manual/reference-command-line.html): base para execução automatizada em batch mode.

## Gate local obrigatório

1. Abrir o projeto com a versão registrada em `ProjectSettings/ProjectVersion.txt`.
2. Aguardar importação e compilação terminarem.
3. Abrir `Assets/Scenes/SampleScene.unity`.
4. Executar `Tools > Game Systems > Validate Current Architecture`.
5. Corrigir todos os itens `[ERRO]`; avisos de hosts não materializados exigem revisão antes de salvar.
6. Executar o checklist `Relatorios/TESTES_POS_GIT_PULL.md`.
7. Confirmar Console sem erros e sem spam de warnings.

## Batch mode

O validador retorna código `1` quando encontra erro crítico e `0` quando não encontra erros:

```bash
Unity -batchmode -nographics -quit \
  -projectPath /caminho/para/MiniMarket \
  -executeMethod GameplayArchitectureValidator.ValidateForCommandLine \
  -logFile validation.log
```

Quando houver suites EditMode/PlayMode, executar também:

```bash
Unity -runTests -batchmode -nographics -quit \
  -projectPath /caminho/para/MiniMarket \
  -testPlatform EditMode \
  -testResults test-results.xml \
  -logFile test-run.log
```

O caminho do executável varia conforme sistema operacional e instalação do Unity Hub.

## Regras para ferramentas de Editor

- nenhuma ferramenta salva cena automaticamente;
- operações destrutivas ou que alterem referência devem registrar Undo;
- não executar materializadores em Play Mode;
- fazer commit antes de materializar hierarquias grandes;
- revisar o diff de `SampleScene.unity` antes de salvar;
- nunca alterar `Assets/Brick Project Studio` por automação.

## Gate de física

O validador considera erro crítico um `MeshCollider` não convexo ligado a `Rigidbody` dinâmico. A correção deve respeitar o objeto:

- cenário estático: remover o `Rigidbody` quando ele não for necessário;
- objeto móvel: usar collider primitivo/composto ou uma malha convexa apropriada;
- não marcar `convex` automaticamente sem revisar a forma e o gameplay.

## Critério de integração

O PR só deve sair de rascunho depois de:

- compilação concluída no Unity;
- validador com `Erros=0`;
- save MMDB2 preservado após dois ciclos de Play;
- menu, compra, interação, jornal e energia verificados;
- Desktop validado e Mobile coberto em aparelho/emulação apropriada;
- diff de cena revisado e sem referências runtime serializadas.
