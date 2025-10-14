# Demo-Aspire-AzureFunction

Démo de Azure Functions, Azure Service Bus, Azurite avec .NET Aspire

TODO: à mettre au propre

Avoir docker

https://github.com/mawax/aspire-demos/tree/main/aspire-functions-service-bus-trigger

https://www.wagemakers.net/posts/aspire-functions-service-bus/#:~:text=In%20this%20quick%20post%20we%20will%20look%20at,Aspire%20Dashboard%20custom%20command%20to%20test%20the%20trigger.

https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=windows%2Cisolated-process%2Cnode-v4%2Cpython-v2%2Chttp-trigger%2Ccontainer-apps&pivots=programming-language-csharp

Ajouter dans le path

Get-Command func | Select-Object -ExpandProperty Source

$funcV4 = "C:\Program Files\Microsoft\Azure Functions Core Tools"
$env:Path = "$funcV4;$env:Path"
func –version

reboot vs


À tester : 

- Changer la structure du projet pour utiliser /src et /tests
- Ajouter des essais unitaires et d'intégration
- 