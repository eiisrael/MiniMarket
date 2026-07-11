# Instruções obrigatórias para agentes e assistentes

Antes de modificar este repositório:

1. Leia `Relatorios/README.md`.
2. Leia `Relatorios/ESTADO_ATUAL.md`.
3. Leia o relatório da área que será alterada.
4. Inspecione os arquivos reais citados nos relatórios.
5. Não trate sistemas marcados como obsoletos como arquitetura ativa.
6. Não altere conteúdo dentro de pastas `Brick Project Studio`, salvo solicitação explícita.
7. Atualize `Relatorios/CHANGELOG_TECNICO.md` e o relatório da área no mesmo trabalho.
8. Use `Relatorios/TESTES_POS_GIT_PULL.md` como critério de conclusão.

## Princípios

- Uma única fonte de verdade para cada dado.
- Nenhuma referência serializada da cena para objetos `DontDestroyOnLoad`.
- Nenhuma gravação em disco por frame.
- Nenhuma busca global ou reflexão no loop normal quando for possível usar cache/eventos.
- Um único controlador autorizado por sistema crítico.
- Compatibilidade Desktop e Mobile deve ser considerada em cada alteração de gameplay/UI/renderização.
- Preservar GUIDs e referências do Unity ao mover ou renomear arquivos.
- Não afirmar que a cena foi testada sem evidência de compilação ou execução.
