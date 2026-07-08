# MiniMarket

![Status](https://img.shields.io/badge/status-em%20desenvolvimento-yellow)
![Engine](https://img.shields.io/badge/engine-Unity-black)
![Language](https://img.shields.io/badge/language-C%23-blue)
![License](https://img.shields.io/badge/license-todos%20os%20direitos%20reservados-red)

## Sobre o projeto

**MiniMarket** é um projeto de jogo/simulação em desenvolvimento, criado com foco em interação com objetos, movimentação em ambiente 3D, sistemas de câmera, HUD e mecânicas inspiradas em um pequeno mercado virtual.

O objetivo do projeto é construir uma experiência interativa onde o jogador possa explorar o ambiente, selecionar objetos, pegar produtos, movimentá-los e interagir com sistemas internos do jogo de forma simples, visual e funcional.

Este repositório também funciona como parte do meu **portfólio de desenvolvimento**, reunindo práticas de programação em Unity, organização de scripts, prototipagem de mecânicas e evolução de sistemas para jogos.

---

## Principais objetivos

- Criar uma cena 3D funcional de mini mercado.
- Desenvolver movimentação de personagem com câmera ajustável.
- Implementar interação com objetos selecionáveis.
- Criar sistemas de mira, mão aberta/fechada e feedback visual no HUD.
- Estruturar scripts de forma organizada e reutilizável.
- Preparar o projeto para futuras integrações com visão computacional, automação e sistemas inteligentes.

---

## Funcionalidades em desenvolvimento

- Sistema de movimentação do jogador.
- Câmera em primeira pessoa/terceira pessoa conforme necessidade da mecânica.
- Mira/ícone de interação com troca visual.
- Mão aberta e mão fechada para ações de pegar objetos.
- Seleção de objetos olhando para eles.
- Sistema para pegar, segurar e mover objetos.
- HUD personalizado com elementos visuais próprios.
- Sistema de áreas/terrenos compráveis no modo BuyScene.
- Organização de scripts por responsabilidade.

---

## Tecnologias utilizadas

- **Unity** — motor principal do projeto.
- **C#** — linguagem usada nos scripts do jogo.
- **Git e GitHub** — versionamento e publicação do projeto.
- **Assets 3D/Low Poly** — estilo visual amigável, leve e adequado para protótipos de jogos.
- **OpenCV / visão computacional** — possibilidade de integração futura para reconhecimento de movimentos e interações inteligentes.

---

## Estrutura esperada do projeto

```text
MiniMarket/
├── Assets/
│   ├── Scripts/
│   │   ├── Player/
│   │   ├── HUD/
│   │   ├── Interaction/
│   │   └── BuyScene/
│   ├── Textures/
│   ├── Sprites/
│   ├── Materials/
│   └── Scenes/
├── ProjectSettings/
├── Packages/
└── README.md
```

> A estrutura pode evoluir conforme o projeto crescer.

---

## Destaques técnicos

### Interação com objetos

O projeto utiliza uma lógica de interação onde o jogador pode olhar para objetos específicos, ativar a mira/mão e executar ações como selecionar, pegar ou movimentar itens dentro da cena.

### HUD e feedback visual

O HUD foi pensado para ser simples, direto e visualmente compatível com um jogo infantil/low poly. Elementos como mão aberta, mão fechada, mira e indicadores ajudam o jogador a entender quando pode interagir com algo.

### Organização modular

Os scripts são planejados para manter responsabilidades separadas, facilitando manutenção e expansão futura do projeto.

---

## Portfólio de trabalhos

Além do **MiniMarket**, também desenvolvo e estudo projetos nas seguintes áreas:

### Desenvolvimento de jogos em Unity

- Prototipagem de jogos 3D.
- Movimentação de personagem.
- Câmera, mira, HUD e interação com objetos.
- Sistemas de seleção, compra de áreas e manipulação de itens.
- Criação de elementos visuais para jogos com estilo low poly/infantil.

### WYD / Cliente Legacy / DX11

- Estudos e desenvolvimento em projetos relacionados a **WYD / With Your Destiny**.
- Modernização de cliente legado.
- Trabalhos com **C++**, **DirectX 9**, **DirectX 11**, UI, renderização, câmera, movimento, inventário, chat e sistemas internos.
- Criação de ferramentas e editores auxiliares para análise, organização e melhoria do cliente.

### WYD Studio Editor

- Projeto de editor standalone em C++/DirectX11.
- Foco em ferramentas de mapa, field, textura, mesh, personagem e animação.
- Conceitos inspirados em fluxos profissionais como Unity/Blender.
- Organização por módulos, viewport 3D, inspector, outliner, backup, undo/redo e edição de assets.

### Automação com IA

- Estudos com automação de vídeos.
- Uso de IA para análise de conteúdo, cortes, transcrição e organização de trechos.
- Fluxos com Python, FFmpeg, SQLite e dashboards.

### Desenvolvimento web e SaaS

- Planejamento de sistemas web com painel administrativo.
- Estruturação de e-commerce/SaaS, cadastro de usuários, produtos, dashboard e fluxo de cliente.
- Estudos envolvendo hospedagem, banco de dados, armazenamento de imagens e boas práticas de LGPD.

### Edição de vídeo e social media

- Criação de materiais para YouTube, Instagram, TikTok e Facebook.
- Edição de vídeos, legendas, cortes, chamadas visuais e organização de conteúdo.
- Produção de artes, slogans e materiais promocionais.

---

## Status do projeto

O projeto está em desenvolvimento ativo.

Algumas mecânicas já foram prototipadas e outras ainda estão sendo planejadas, testadas ou melhoradas. O foco atual é evoluir a base do jogo, melhorar a organização dos scripts e deixar o projeto pronto para novas funcionalidades.

---

## Próximas melhorias planejadas

- Melhorar o sistema de interação com produtos.
- Criar inventário ou carrinho de compras.
- Desenvolver sistema de compra/venda dentro do mini mercado.
- Melhorar animações de mão, cursor e HUD.
- Criar mais feedback visual para objetos selecionáveis.
- Otimizar scripts e separar responsabilidades.
- Preparar integração futura com IA/visão computacional.
- Melhorar organização visual das cenas e prefabs.

---

## Como executar

1. Clone o repositório:

```bash
git clone https://github.com/eiisrael/MiniMarket.git
```

2. Abra o projeto no **Unity Hub**.
3. Selecione a versão compatível do Unity usada no projeto.
4. Abra a cena principal em `Assets/Scenes`.
5. Execute o projeto pelo botão **Play** no editor.

---

## Autor

Desenvolvido por **Erick Israel**.

GitHub: [@eiisrael](https://github.com/eiisrael)

---

## Direitos autorais

Copyright © 2026 Erick Israel.

Todos os direitos reservados.

Este projeto, seus códigos, ideias, organização, sistemas, imagens, elementos visuais, documentação e demais arquivos autorais pertencem ao autor, salvo quando indicado o uso de assets de terceiros com licenças próprias.

A disponibilização deste repositório no GitHub não concede permissão automática para copiar, vender, redistribuir, modificar, publicar, reutilizar comercialmente ou utilizar partes deste projeto em outros trabalhos sem autorização prévia do autor.

Qualquer uso não autorizado poderá violar direitos autorais e demais direitos aplicáveis.

Assets, imagens, modelos, sons ou bibliotecas de terceiros, quando utilizados, continuam pertencendo aos seus respectivos autores e devem respeitar suas licenças originais.

---

## Observação

Este repositório tem finalidade de desenvolvimento, estudo, demonstração técnica e portfólio profissional.
