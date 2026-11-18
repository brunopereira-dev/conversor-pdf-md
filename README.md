# conversor-pdf-md

Aplicação desktop em .NET que converte PDFs em Markdown pensado para **RAG**: a saída serve de base de conhecimento para um LLM, então o critério é **fidelidade textual e navegabilidade**, não estética.

Um LLM não "lê" PDF. Ele lê texto. Se o texto chega com cabeçalhos repetidos, hifenização quebrada, colunas intercaladas e sem referência de página, o RAG recupera trechos ruins e não consegue citar a fonte. Este conversor resolve a etapa de ingestão.

## O que ele faz

- **Documentos lógicos:** junta vários PDFs num único `.md` (por exemplo, um manual dividido em volumes), na ordem definida pelo usuário.
- **Front matter YAML** com título, versão, tipo, escopo, idioma, dependência entre documentos e as partes de origem com a contagem de páginas.
- **Marcador de página** ao fim de cada página (`<!-- p.12 -->`, ou `<!-- p.12 | parte1 -->` em documentos mesclados), para o LLM citar a fonte com precisão.
- **Sumário** com âncoras e a página de início de cada seção, preservando a numeração original dos títulos.
- **Limpeza de ruído:** remove cabeçalhos e rodapés repetidos, números de página soltos e hifenização de quebra de linha, e reúne parágrafos quebrados.
- **Ordem de leitura** em páginas com várias colunas.
- **Tabelas** convertidas para tabela Markdown.
- **Figuras** marcadas como `> [FIGURA p.N]`, com a legenda encontrada no texto.
- **Trechos ilegíveis** marcados explicitamente em vez de adivinhados.
- **`INDEX.md`** listando todos os documentos gerados.
- **Sugestão de metadados** (título e versão) a partir das primeiras páginas do PDF. Ela é só uma sugestão: o usuário confirma.

## Validação da saída

Depois de gerar os arquivos, a aplicação verifica e mostra um relatório:

1. O número de marcadores de página é igual a `total_paginas`.
2. Os marcadores estão em sequência, sem lacunas nem repetições.
3. Todo título do sumário existe no corpo.
4. Nenhuma tabela tem número de colunas inconsistente entre as linhas.
5. Todo `.md` está no `INDEX.md`, e todo link do índice aponta para um arquivo existente.
6. A soma das páginas das partes é igual a `total_paginas`.
7. Em documentos mesclados, a numeração de cada parte é contínua e reinicia em 1.
8. Todo PDF selecionado foi consumido por algum documento.

Se um documento falhar, os outros continuam sendo processados e o erro aparece no relatório.

## Exemplo de saída

```markdown
---
titulo: "Manual do Equipamento X"
versao: "V1.2"
tipo: "manual base"
idioma: "pt"
partes:
  - arquivo: "manual-parte1.pdf"
    paginas: 48
total_paginas: 48
---

# Manual do Equipamento X

## Sumário
- [1 Introdução](#1-introducao) (p.3)

## 1 Introdução

Texto da seção, com o parágrafo reunido numa linha só.

<!-- p.3 -->
```

O formato completo está em [SPEC-pdf-to-md.md](SPEC-pdf-to-md.md).

## Arquitetura

Clean Architecture com as camadas separadas em pastas, num único projeto:

| Pasta | Conteúdo | Depende de |
|---|---|---|
| `Domain/` | Entidades e enums | Nada |
| `Application/` | Casos de uso, interfaces, análise de página e geração do Markdown | `Domain` |
| `Infrastructure/` | Leitura de PDF ([PdfPig](https://github.com/UglyToad/PdfPig)) e escrita em disco | `Application`, `Domain` |
| `Presentation/` | Interface em WinForms | `Application` |

A composição das dependências fica no `Program.cs`, o único ponto que conhece a `Infrastructure`.

## Como rodar

Requisitos: Windows e [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/brunopereira-dev/conversor-pdf-md.git
cd conversor-pdf-md
dotnet run --project src/PdfToMarkdown
```

Na janela:
1. Escolha a **pasta de saída**.
2. Use **Adicionar arquivo** para montar cada documento. A ordem das linhas é a ordem de concatenação.
3. Confira os metadados sugeridos e clique em **Converter**.

## Limitações

- **Só Windows**, porque a interface é WinForms. Não há CLI.
- **Sem OCR:** PDFs escaneados (imagem sem camada de texto) geram trechos marcados como ilegíveis.
- **Figuras:** a aplicação não "enxerga" a imagem. Ela registra a figura e a legenda encontrada no texto, mas não descreve o conteúdo visual.
- Ainda não há testes automatizados.

## Como foi feito

O projeto foi construído com **desenvolvimento orientado a especificação**, com IA:
- [SPEC-pdf-to-md.md](SPEC-pdf-to-md.md) define o formato de saída e o comportamento da interface;
- [CLAUDE.md](CLAUDE.md) define as restrições de arquitetura e de código;
- a implementação foi gerada com o [Claude Code](https://claude.com/claude-code) a partir dessas duas fontes, e revisada por mim.

A lição principal foi que a qualidade do resultado acompanha a precisão da especificação: um formato bem definido e validações objetivas valem mais do que um prompt longo.
