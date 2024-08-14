# Code Echo AI
An AI App to fix Sonar Qube Issues via Ollama
![Logo](./CodeEcho.svg)


## Installation
1. Install dotnet-format: dotnet tool install -g dotnet-format
2. Start Docker
3. Run Ollama: docker run -d -v ollama:/root/.ollama -p 11434:11434 --name ollama ollama/ollama
4. Pull llama3 model: docker exec -it ollama /bin/bash -c "ollama pull llama3"
5. Configure appsettings.json with url of Sonar, an API token and the project key. Optionally configure the Ollama url.
