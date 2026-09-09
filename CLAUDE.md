# conversor-pdf-md

Aplicação desktop que converte PDFs em Markdown. A saída alimenta um LLM como
base de conhecimento (RAG), então o critério é fidelidade textual e
navegabilidade — não estética visual.

**`SPEC-pdf-to-md.md` é a fonte de verdade do formato de saída.** Consulte antes
de mexer em qualquer coisa que gere ou valide `.md`. Front matter, marcadores de
página, tabelas, figuras, `INDEX.md`, comportamento da interface e as 8
validações obrigatórias estão especificados lá em detalhe. Não improvise
formato.

- Alvo: .NET 10, WinForms (`net10.0-windows`), projeto único.
- Entrada: seleção manual de arquivos pela interface. Não há diretório de
  entrada, não há CLI.

## Restrições

### Idioma
- Identificadores, nomes de arquivos, pastas e comentários em **inglês**.
- Strings de saída e rótulos de interface em **português**, exatamente como
  definidos na spec (`titulo`, `versao`, `[FIGURA p.N]`, `(não informado)`,
  `Adicionar arquivo`). Nunca traduzir esses literais.

### Arquitetura
Clean Architecture com camadas em pastas, num único projeto:

| Pasta | Conteúdo | Depende de |
|---|---|---|
| `Domain/` | entidades e enums | nada |
| `Application/` | casos de uso, interfaces, DTOs | `Domain` |
| `Infrastructure/` | implementações de I/O: leitura de PDF, escrita em disco | `Application`, `Domain` |
| `Presentation/` | Forms | `Application` |

- A composição das dependências fica no `Program.cs`, na raiz do projeto —
  único ponto que conhece `Infrastructure`.

### SOLID
- Um caso de uso por operação (SRP).
- A camada de apresentação depende apenas da abstração declarada em
  `Application` (DIP).
- Interfaces pequenas e específicas (ISP).
- Sem regras de negócio na camada de apresentação.

### Convenções de código
- Primary constructor nas classes de `Application` e `Infrastructure`.
  **Não** nos Forms: o designer do Visual Studio exige construtor sem
  parâmetros.
- Entrada e saída de caso de uso como `sealed record`.
- Nunca expor entidade de domínio fora de `Application`.

### Validação
- Campos de formulário: DataAnnotations. Não adicionar pacotes de validação —
  a restrição de pacotes vale **apenas** para validação; bibliotecas de leitura
  de PDF são permitidas e necessárias.
- As 8 validações obrigatórias da spec não são atributos: são um serviço em
  `Application` que roda sobre a saída gerada e devolve relatório.

### Assincronismo
- Todo método assíncrono recebe e repassa `CancellationToken`.
- Nunca `.Result`, `.Wait()` ou `.GetAwaiter().GetResult()` — sempre `await`.
- `async void` é proibido, exceto em handlers de evento, onde a assinatura
  exige. Nesses casos, envolver o corpo em `try/catch`.
- Progresso de tarefa em segundo plano é reportado por `IProgress<T>`, com a
  instância de `Progress<T>` criada na thread de UI.
