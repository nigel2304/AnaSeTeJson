Este projeto gera um json e xml com informações sobre transições de status das issues no jira, bem como calcula o cycletime considerando dias seguindo e úteis.
Para que possa funcionar é necessário aplicar uma requisição via get no postman e gerar um json com nome SourceEstoriasJiraAPI.
Salve o arquivo no mesmo diretório do projeto e execute o mesmo.

Exemplo requisição Get: SUA_URL_JIRA/rest/api/2/search?jql=project=ID_SEU_PROJETO and issuetype=Estória and status not in (Backlog, Cancelado) order by Key&maxResults=200&expand=changelog&fields=summary,resolved,customfield_16702,customfield_16701,assignee,issuetype,resolutiondate
