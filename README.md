# DatabaseToCsv 3.0

Baseado no projeto original [Database-to-CSV](https://github.com/brunossn/Database-to-CSV), foi desenvolvida a nova versão **DatabaseToCsv 3.0**, trazendo melhorias de compatibilidade, novos formatos de exportação e maior confiabilidade no processamento de dados.

## Sobre o projeto

O **DatabaseToCsv 3.0** é uma evolução moderna da versão original 1.0, desenvolvido em C#, com foco em performance, compatibilidade com drivers atuais e novos recursos para exportação de dados.

---

## Principais melhorias da versão 3.0

- Atualização dos conectores e drivers para versões mais recentes
- Melhor compatibilidade com bancos de dados modernos
- Novo suporte à exportação em formato **Parquet**
- Compressão de arquivos Parquet para melhor performance e redução de tamanho
- Implementação de cabeçalho automático em arquivos CSV/TXT
- Geração automática de arquivos `.error` em caso de falhas durante processamento
- Novo ícone e identidade visual modernizada

---

## Bancos de dados suportados

- SQL Server
- SQLite
- Firebird
- Oracle
- MySQL
- Microsoft Access
- IBM DB2
- PostgreSQL

---

## Recursos disponíveis

### Exportação de dados

- CSV
- TXT
- Parquet compactado

### Tratamento de erros

Agora o sistema possui controle de falhas durante execução:

- Registro de erros
- Arquivos `.error` gerados automaticamente
- Melhor rastreabilidade e diagnóstico de problemas

### Estrutura de saída aprimorada

- Inclusão automática de cabeçalhos em arquivos CSV/TXT
- Melhor organização dos dados exportados

---

## Tecnologias utilizadas

- C#
- .NET
- CSV Export
- Apache Parquet
- Compressão de arquivos

---

## Evolução do projeto

| Versão | Descrição |
|---|---|
| 1.0 | Exportação básica para CSV |
| 3.0 | Drivers atualizados, suporte Parquet, compressão, cabeçalhos e melhorias visuais |

---

## Projeto original

Projeto base utilizado como referência:

https://github.com/brunossn/Database-to-CSV