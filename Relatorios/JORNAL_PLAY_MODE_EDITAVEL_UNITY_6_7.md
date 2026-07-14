# Jornal editável no Play Mode — Unity 6.7

Atualizado em: 2026-07-14

## Objetivo

Permitir que valores alterados manualmente no Inspector ou pelos gizmos durante o Play Mode sejam reaplicados à cena quando o Editor voltar para Stop.

A cobertura inclui objetos e componentes existentes sob:

```text
Newspaper_Stand
Newspaper_InteractionPrompt
Jornal_Place
Put_Area
Newspaper_PlacePrompt
NewspaperPutArea_*
Placed_Newspaper_Runtime
```

## Arquivo responsável

```text
Assets/Editor/ProjectMaintenance/NewspaperPlacePromptScalePersistence.cs
```

A classe ativa é:

```text
NewspaperPlayModeHierarchyPersistence
```

## Compatibilidade com Unity 6.7

A versão anterior não compilava porque:

- `UnityEngine.Object.GetInstanceID()` passou a ser obsoleto com erro de compilação;
- `PropertyModification.Apply()` não existe na API pública disponível.

A implementação atual:

- usa identidade por referência somente durante a sessão de Play;
- usa cena, caminho de sibling, tipo e índice do componente para localizar o objeto real após Stop;
- captura valores tipados por `SerializedProperty`;
- reaplica valores por `SerializedObject` e `ApplyModifiedPropertiesWithoutUndo()`;
- mantém suporte ao Undo;
- marca a cena como modificada, mas não salva automaticamente;
- não usa busca, reflexão ou gravação por frame.

## Propriedades cobertas

- inteiros, booleanos, floats e strings;
- cores;
- referências para assets e objetos da cena;
- enums;
- `Vector2`, `Vector3`, `Vector4`;
- `Rect`, `Bounds`, `Quaternion`;
- `Vector2Int`, `Vector3Int`, `RectInt`, `BoundsInt`;
- curvas de animação;
- `Transform`, `RectTransform`, UI, TextMeshPro, renderers, linhas e componentes serializados do jornal.

## Regra de persistência

Somente alterações manuais registradas pelo Undo do Editor são copiadas. Estados automáticos de gameplay não viram configuração permanente.

Objetos e componentes precisam existir antes de entrar no Play Mode. Criação, exclusão, reordenação, troca de parent e adição/remoção de componentes devem ser feitas em Stop.

## Teste local obrigatório

1. Aguardar o Unity compilar sem erros.
2. Entrar no Play Mode.
3. Alterar posição, rotação e escala de diferentes objetos do jornal.
4. Alterar cor, transparência, texto e tamanho de elementos do prompt.
5. Pressionar Stop.
6. Confirmar a mensagem `[NewspaperPlayEdit]` no Console.
7. Confirmar que os valores alterados continuam no Inspector.
8. Salvar a cena com `Ctrl+S`.

A validação feita no repositório é estática. A compilação e o comportamento final devem ser confirmados no Unity local.
