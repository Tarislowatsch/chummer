# Mono toolchain and runtime for building and running Chummer on Linux.
# mono:6.12 is Debian buster: its packages live on archive.debian.org now.
FROM mono:6.12
RUN printf 'deb http://archive.debian.org/debian buster main\ndeb http://archive.debian.org/debian-security buster/updates main\n' > /etc/apt/sources.list \
 && rm -f /etc/apt/sources.list.d/mono-official-stable.list \
 && apt-get -o Acquire::Check-Valid-Until=false update \
 && apt-get install -y --no-install-recommends libgdiplus fonts-dejavu-core \
 && rm -rf /var/lib/apt/lists/*
