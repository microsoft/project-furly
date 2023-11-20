FROM ubuntu:16.04

RUN apt-get update -qq && apt-get install -qqy \
    apt-transport-https \
    apt-utils \
    ca-certificates \
    mosquitto-clients \
    curl \
    wget \
    gnupg \
    lsb-release \
    jq \
    net-tools \
    iptables \
    iproute2 \
    systemd && \
    rm -rf /var/lib/apt/lists/*

RUN AZ_REPO=$(lsb_release -cs) && \
    echo "deb [arch=amd64] https://packages.microsoft.com/repos/azure-cli/ $AZ_REPO main" | \
    tee /etc/apt/sources.list.d/azure-cli.list && \
    curl -L https://packages.microsoft.com/keys/microsoft.asc | apt-key add -

RUN curl https://packages.microsoft.com/config/ubuntu/16.04/prod.list > ./microsoft-prod.list && \
    cp ./microsoft-prod.list /etc/apt/sources.list.d/ && \
    curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.gpg && \
    cp ./microsoft.gpg /etc/apt/trusted.gpg.d/ 

RUN apt-get update && apt-get install -y --no-install-recommends \
    azure-cli \
    moby-cli \
    moby-engine && \ 
    rm -rf /var/lib/apt/lists/*

RUN curl -L https://github.com/Azure/azure-iotedge/releases/download/1.2.0-rc1/libiothsm-std_1.2.0_rc1-1_ubuntu16.04_amd64.deb -o libiothsm-std.deb && \
    dpkg -i ./libiothsm-std.deb && \
    curl -L https://github.com/Azure/azure-iotedge/releases/download/1.2.0-rc1/iotedge_1.2.0_rc1-1_ubuntu16.04_amd64.deb -o iotedge.deb && \
    dpkg -i ./iotedge.deb

RUN az extension add --name azure-cli-iot-ext

COPY entrypoint.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/entrypoint.sh

VOLUME /var/lib/docker

EXPOSE 2375
EXPOSE 15580
EXPOSE 15581
EXPOSE 8883
EXPOSE 1883
EXPOSE 5276

ENTRYPOINT ["bash", "entrypoint.sh"]

CMD []