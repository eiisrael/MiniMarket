# Energia com degradê, porcentagem e pulsação

Atualizado em: 2026-07-13

## Alterações

- o texto da energia é reforçado no formato `98%`;
- o raio troca entre `green_energy`, `yellow_energy` e `red_energy`;
- a progress bar usa transição suave de cor ao redor de 61% e 25%;
- o raio pulsa como batimento de coração quando o personagem começa a correr ou quando Shift está pressionado;
- a escala original do raio é restaurada suavemente ao parar de correr.

## Arquivos

```text
Assets/Scripts/UI/MiniMarketEnergyVisualEffects.cs
Assets/Editor/ProjectMaintenance/EnergyVisualEffectsSetup.cs
```

## Faixas do raio

```text
100% a 61%: green_energy.png
60% a 26%: yellow_energy.png
25% a 0%: red_energy.png
```

## Degradê

O ícone troca de sprite exatamente pelas faixas acima. A barra usa `Color.Lerp` com curva
suave ao redor dos limites. A largura da transição é editável em `Largura Transicao`.

## Pulsação

Campos editáveis:

```text
Pulsar Ao Correr
Detectar Shift Como Fallback
Intensidade Pulsacao
Batimentos Por Minuto
Segundo Batimento
Retorno Escala
```

A animação usa dois pulsos curtos por ciclo para lembrar um batimento cardíaco.

## Ferramentas

Fora do Play Mode:

```text
Tools > MiniMarket > Aplicar Efeitos Visuais da Energia
Tools > MiniMarket > Validar Efeitos Visuais da Energia
```

A ferramenta preserva o layout salvo, encontra o objeto `Image` do raio, liga os três sprites,
configura o texto percentual, adiciona o componente visual e salva a cena.

## Testes locais

1. confirmar `100%` ao iniciar;
2. confirmar `%` durante toda a descarga;
3. confirmar raio verde acima de 60%;
4. confirmar raio amarelo entre 60% e 26%;
5. confirmar raio vermelho em 25% ou menos;
6. confirmar transição suave da cor da barra;
7. pressionar Shift e confirmar pulsação do raio;
8. soltar Shift e confirmar retorno suave à escala original;
9. confirmar zero erros vermelhos no Console.

A compilação e o teste visual final dependem do Unity local.
