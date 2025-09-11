# CP Async C#
## Participantes do Grupo
- Ricardo Ramos Vergani RM550166
- Arthur Baldissera RM550219

## Mudancas realizadas: 
- Uso do Await inves de Response
- Funcoes Asincronas
- Versao Assincrona do File.ReadAllLines
- Loops Assincronos (Agora permitindo a utilizacao do Await dentro deles)
- Hash por Municipio com uso de TaskWHenAll e Task.Run para paralelizacao
- Versoes assincronas de escrita de arquivos
- Logica completa com Await




## Impactos no tempo de execucao
O tempo de execucao caiu drasicamente, de 50 segundos para apenas 4/5, devido ao uso do Async, esse tempo, falando em projetos reais e escalaveis, e gigantesco, podendo realizar diversas consultas no tempo de que antes seria apenas uma, mostra a eficiencia e programacao limpa de maneira fluida.



# CP Async C# PARTE 2

## Mudancas realizadas: 
- Antes de baixar o CSV verifica se ja existe municipios.csv na pasta local
- Calcula o HASH-256 do arquivo baixado ( Se forem iguais, mantem, diferentes, troca)
- Exporta o .bin alem do JSON e CSV, que fica salvo dentro da pasta do projeto.
- Pesquisas por UFs, nomes ou codigos para verificar seus municipios
- Adicao de diversas features novas sem comprometer o tempo de processamento do programa
- Preza sempre memoria e velocidade

## Impactos no tempo de execucao
O tempo de execucao se manteu constante sem muitas alteracoes desde a primeira modificacao do projeto. Porem, com mais recursos, como pesquisas e formas de otimizacao, nao cair e nem subir, é uma vitoria importantissima, sempre mantendo o codigo assincrono. 



