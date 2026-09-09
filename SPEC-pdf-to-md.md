# SPEC — Conversor PDF → Markdown

Criar um conversor de PDF → Markdown. A saída será consumida por um LLM como
base de conhecimento (RAG / contexto). Otimize para **fidelidade textual e
navegabilidade**, não para estética visual.

---

## Entrada

### Origem

Origem única: **seleção manual** de arquivos pela interface gráfica. Não há
diretório de entrada nem descoberta automática de arquivos.

- Quantidade dinâmica: N arquivos, definidos em tempo de execução pelo
  usuário — nunca hardcoded.
- Somente arquivos `.pdf`.

### Documento lógico

O usuário organiza os arquivos selecionados em **documentos lógicos**. Um
documento lógico é um conjunto ordenado de PDFs somado aos metadados que o
descrevem, e produz exatamente um `.md` de saída.

- Documento com uma única parte: equivale ao modo 1:1.
- Documento com várias partes: concatena os PDFs na ordem definida (útil para
  manuais divididos em volumes).
- Todo arquivo selecionado pertence a exatamente um documento lógico.

O modelo de dados de um documento lógico corresponde a:

```yaml
documentos:
  - saida: "ui24r-manual-base.md"
    titulo: "Soundcraft Ui24R — User Guide"
    versao: "V1.2"
    fonte_url: "https://..."
    tipo: "manual base"
    escopo: "operação geral, conexões, mixer, roteamento"
    depende_de: null
    partes:
      - arquivo: "ui24r_guide_parte1.pdf"
      - arquivo: "ui24r_guide_parte2.pdf"
  - saida: "ui24r-addendum.md"
    titulo: "Soundcraft Ui24R — Addendum"
    versao: "V3"
    tipo: "addendum"
    escopo: "recursos novos do firmware V3"
    depende_de: "ui24r-manual-base.md"
    partes:
      - arquivo: "ui24r_addendum_v3.pdf"
```

- A ordem em `partes` define a ordem de concatenação.

---

## Interface gráfica

A janela é composta por uma lista de **documentos**. Cada documento reúne seus
metadados e sua lista ordenada de arquivos.

### Estado inicial

- A tela inicia com um documento vazio: metadados em branco e um único botão
  `Adicionar arquivo`.
- Um botão `Adicionar documento` cria documentos adicionais.
- Um seletor `Pasta de saída` define onde os `.md` e o `INDEX.md` serão
  gravados. Obrigatório.

### Seleção de arquivos

Dentro de cada documento:

- O clique em `Adicionar arquivo` abre um diálogo restrito a **um** arquivo
  `.pdf`.
- Confirmada a seleção:
  - o nome do arquivo passa a ser exibido à direita daquele botão;
  - um novo botão `Adicionar arquivo`, ainda vazio, é renderizado
    imediatamente abaixo.
- O ciclo se repete: existe sempre exatamente **um** botão vazio por documento,
  e ele é sempre o último da lista.
- Clicar num botão que já tem arquivo reabre o diálogo e **substitui** o
  arquivo daquela linha — não gera botão novo.
- Selecionar um arquivo já presente em qualquer documento gera aviso e é
  rejeitado: o mesmo PDF não pode ocupar duas linhas.

### Remoção e reordenação

Cada linha preenchida exibe, à direita do nome do arquivo:

| Controle | Ação |
|---|---|
| `↑` | move a linha uma posição acima |
| `↓` | move a linha uma posição abaixo |
| `✕` | remove a linha |

- `↑` fica desabilitado na primeira linha; `↓`, na última linha preenchida.
- Remover uma linha faz as seguintes subirem — não fica lacuna.
- O botão vazio permanece sempre na última posição e não exibe controles.
- Removidas todas as linhas, o documento volta ao estado inicial: um único
  botão vazio.
- A ordem visual das linhas, de cima para baixo, é a ordem de concatenação
  (`partes` no modelo de documento lógico).

Documentos também podem ser removidos e reordenados entre si. Remover um
documento descarta seus arquivos da execução; a ordem entre documentos afeta
apenas a listagem do `INDEX.md`.

### Metadados do documento

Cada documento expõe os campos abaixo, todos editáveis:

| Campo | Controle | Preenchimento inicial |
|---|---|---|
| `saida` | texto | derivado de `titulo`, em kebab-case sem acentos |
| `titulo` | texto | sugerido a partir do 1º PDF (ver abaixo) |
| `versao` | texto | sugerido a partir do 1º PDF (ver abaixo) |
| `tipo` | texto | vazio |
| `escopo` | texto | vazio |
| `fonte_url` | texto | vazio |
| `depende_de` | lista suspensa com os demais documentos da execução, mais a opção `nenhum` | `nenhum` |

Regra de nomeação da saída:

- `saida` é derivado de `titulo`, em kebab-case e sem acentos.
- Se `titulo` não for preenchido, usar `arquivo-sem-titulo`.
- Havendo colisão de nome entre documentos da mesma execução, acrescentar
  sufixo numérico incremental — `arquivo-sem-titulo-2.md`,
  `arquivo-sem-titulo-3.md`. Nunca sobrescrever um arquivo já gerado na
  mesma execução.

### Sugestão automática de metadados

`titulo` e `versao` podem ser **sugeridos**, nunca assumidos:

- `titulo`: primeiro valor não vazio entre — `/Title` dos metadados do PDF,
  texto de maior corpo tipográfico da p.1, raiz dos bookmarks, nome do arquivo
  sem extensão.
- `versao`: primeira ocorrência de padrão de versão (`V1.2`, `Rev. 3`,
  `Version 2.1`) na capa ou no rodapé.

Regras:

- O valor sugerido aparece no campo já editável, marcado visualmente como
  sugestão até que o usuário o confirme ou altere.
- Campo não confirmado e não editado é gravado como `(não informado)`.
- `tipo`, `escopo`, `fonte_url` e `depende_de` **nunca** são sugeridos — não
  são deriváveis do conteúdo do PDF.

### Conversão

- `Converter` fica desabilitado enquanto houver documento sem nenhum arquivo,
  ou enquanto a pasta de saída não estiver definida.
- Durante a execução a tela exibe o progresso e, ao final, o relatório previsto
  em `## Requisitos gerais`.

---

## Saída

- Um `.md` por **documento lógico** (não por PDF).
- **Sempre** um `INDEX.md`, independentemente da quantidade de documentos.

---

## Especificação do arquivo de saída

### 1. Bloco de metadados (primeiras linhas, front matter YAML)

```yaml
---
titulo: "Soundcraft Ui24R — User Guide"
versao: "V1.2"
fonte_url: "https://..."
tipo: "manual base"
escopo: "operação geral, conexões, mixer, roteamento"
depende_de: null
idioma: "en"
extraido_em: "2026-09-04"
partes:
  - arquivo: "ui24r_guide_parte1.pdf"
    paginas: 48
  - arquivo: "ui24r_guide_parte2.pdf"
    paginas: 35
total_paginas: 83
---
```

No modo 1:1, `partes` contém um único item.

### 2. Sumário

Logo após o front matter, gerar índice com links de âncora para todos os
títulos `##` e `###`, indicando a página de início de cada um.

### 3. Hierarquia de títulos

- Espelhar a estrutura real do documento.
- **Preservar a numeração original** das seções no título
  (ex: `### 4.2 Recording`). Nunca renumerar.
- Não inventar seções que não existem no PDF.
- Nível `#` reservado para o título do documento (usar apenas uma vez).

### 4. Marcadores de página

Inserir ao **final** do conteúdo de cada página:

```markdown
<!-- p.12 -->
```

Regra crítica: um marcador por página, sempre, inclusive em páginas que
contenham apenas imagem. Isso permite citação de fonte precisa.

### 5. Limpeza de ruído

Remover:
- Cabeçalhos e rodapés repetidos em todas as páginas
- Números de página soltos (já substituídos pelo marcador `<!-- p.N -->`)
- Marcas d'água, textos de copyright repetidos
- Hifenização de quebra de linha: `configura-\nção` → `configuração`
- Quebras de linha no meio de frases (reunir o parágrafo em linha única)

Manter:
- Quebras de parágrafo reais (linha em branco)
- Notas de rodapé — mover para o fim da seção, prefixadas com `> Nota:`

### 6. Layout em colunas

Se a página tiver múltiplas colunas, ler na **ordem lógica de leitura**
(coluna 1 completa, depois coluna 2). Nunca intercalar linhas de colunas
diferentes.

### 7. Tabelas

- Converter **sempre** para tabela Markdown com cabeçalho e separador.
- Nunca transformar tabela em texto corrido ou lista.
- Célula mesclada: repetir o valor nas células correspondentes.
- Célula vazia: deixar vazia (não escrever "N/A").
- Se a tabela for grande demais para o formato Markdown, precedê-la de
  `> [TABELA p.N: <descrição>]` e usar bloco de código com alinhamento fixo.

### 8. Figuras, diagramas e screenshots

O LLM não enxerga imagens. Para cada figura:

```markdown
> [FIGURA p.9] Diagrama do painel traseiro.
> Legenda original: "1. Power inlet  2. XLR out L/R  3. USB host ..."
> Descrição: lista dos conectores da esquerda para a direita, com rótulos
> numerados conforme a legenda.
```

- **Transcrever integralmente** legendas, callouts e qualquer texto
  embutido na imagem (rótulos de botões, valores em telas).
- Não descrever aparência estética (cores, sombras) — apenas informação
  funcional.

### 9. Fidelidade terminológica

- Preservar **literalmente** nomes de botões, menus, abas e parâmetros:
  `Mix`, `FX`, `Gate`, `Show`, `Snapshot`.
- Preservar unidades e valores exatos: `48 kHz`, `-18 dBFS`, `+48V`.
- Envolver esses elementos em backticks quando forem controles de interface.
- **Nunca parafrasear, resumir, traduzir ou corrigir** o texto original.
- Manter o idioma original do PDF.

### 10. Elementos especiais

| Elemento no PDF | Representação em Markdown |
|---|---|
| Aviso / Warning / Caution | `> ⚠️ **AVISO:** <texto>` |
| Nota / Tip | `> **Nota:** <texto>` |
| Lista numerada de passos | lista ordenada `1.` `2.` `3.` |
| Atalho de teclado | `` `Ctrl + S` `` |
| Valor de display / LCD | bloco de código |

### 11. Trechos ilegíveis

Se a extração falhar em algum ponto, marcar explicitamente em vez de
adivinhar o conteúdo:

```markdown
> [ILEGÍVEL p.22: texto não extraído — verificar no PDF original]
```

### 12. Numeração de páginas em documentos mesclados

Quando um documento lógico vem de múltiplos PDFs, o marcador de página deve
identificar o arquivo de origem:

```markdown
<!-- p.12 | parte1 -->
```

- A numeração **reinicia** conforme cada PDF de origem (não é contínua).
- O rótulo da parte é o nome do arquivo sem extensão.
- No modo 1:1 (documento com uma única parte), usar o formato simples:
  `<!-- p.12 -->`.
- Ao iniciar cada parte, inserir um delimitador:
  `<!-- INÍCIO PARTE: ui24r_guide_parte2.pdf -->`

### 13. Arquivo `INDEX.md`

Gerado sempre, na raiz do diretório de saída. Serve como ponto de entrada
para o LLM decidir **onde procurar** antes de responder.

```markdown
# Índice da Base de Conhecimento

Gerado em: 2026-09-04
Documentos: 2 | Páginas totais: 83

## Documentos

### [ui24r-manual-base.md](./ui24r-manual-base.md)
- **Tipo:** manual base (documento de referência principal)
- **Versão:** V1.2 | **Páginas:** 83
- **Escopo:** operação geral, conexões, mixer, roteamento
- **Origem:** `ui24r_guide_parte1.pdf`, `ui24r_guide_parte2.pdf`
- **Seções de nível 2:**
  - 1. Introduction
  - 2. Hardware Overview
  - 3. Mixing
- **Depende de:** —

### [ui24r-addendum.md](./ui24r-addendum.md)
- **Tipo:** addendum (descreve apenas mudanças/adições)
- **Versão:** V3 | **Páginas:** 35
- **Escopo:** recursos novos do firmware V3
- **Origem:** `ui24r_addendum_v3.pdf`
- **Seções de nível 2:**
  - 1. What's New
  - 4. Recording
- **Depende de:** `ui24r-manual-base.md`

## Como consultar

1. Identifique o documento pelo escopo acima.
2. Para addenda, consultar **também** o documento base indicado em
   "Depende de".
3. Em caso de conflito, o documento de maior versão / addendum prevalece
   sobre o manual base.
4. Citar sempre a fonte usando o marcador de página: `(p.12, parte1)`.

## Mapa de tópicos

| Tópico | Documento | Seção | Página |
|---|---|---|---|
| Gravação multitrack | ui24r-addendum.md | 4.2 Recording | p.14 |
| Phantom power | ui24r-manual-base.md | 2.3 Inputs | p.9, parte1 |
```

**Regras do `INDEX.md`:**
- `Tipo`, `Escopo` e `Depende de` vêm dos metadados do documento definidos na
  interface; se não preenchidos, escrever `(não informado)`. Nunca inferir a
  partir do conteúdo do PDF.
- `Seções de nível 2` extraídas automaticamente do sumário de cada `.md`.
- `Mapa de tópicos`: gerado a partir dos títulos de nível 3, limitado aos
  40 mais relevantes. Se não for viável, omitir a tabela inteira em vez de
  produzi-la incompleta.

---

## Requisitos gerais

- Encoding **UTF-8**.
- Sem tags HTML, exceto os comentários `<!-- ... -->` especificados.
- Nenhum conteúdo do PDF pode ser descartado silenciosamente.
- Nomes de arquivo de saída em `kebab-case`, sem acentos.
- Falha ao processar um documento não deve abortar os demais: registrar o erro
  e continuar.
- Ao final, exibir relatório na interface: documentos gerados, páginas
  processadas, nº de tabelas, nº de figuras, nº de trechos ilegíveis, avisos.

## Validação obrigatória

Após gerar os arquivos, verificar e reportar:

1. A quantidade de marcadores de página é igual a `total_paginas`.
2. Os marcadores estão em sequência, sem lacunas nem repetições.
3. Todo título do sumário existe no corpo do documento.
4. Nenhuma tabela ficou com número de colunas inconsistente entre linhas.
5. Todo `.md` gerado está listado no `INDEX.md`, e todo link do `INDEX.md`
   aponta para um arquivo existente.
6. A soma de `paginas` das partes é igual a `total_paginas`.
7. Em documentos mesclados, a sequência de páginas de cada parte é contínua
   e reinicia em 1.
8. Todo PDF selecionado na interface foi consumido por algum documento lógico.
