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


## OBS: Entrega alguns minutos atrasado, havia te avisado que ia enviar o link quando chegasse em casa! Perdao a demora. (Checar Timing do Commit) 
