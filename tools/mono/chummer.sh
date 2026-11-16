#!/bin/sh
# Build and run Chummer under Mono in Docker, on the host's X server.
#   tools/mono/chummer.sh build          build the solution (Debug)
#   tools/mono/chummer.sh run [file]     start Chummer, optionally opening a .chum file
set -eu
repo=$(cd "$(dirname "$0")/../.." && pwd)
image=chummer-mono
docker build -q -t "$image" "$repo/tools/mono" >/dev/null

# - host uid: files saved from Chummer belong to the user
# - host home: Mono keeps Chummer's options in ~/.mono
dock() {
	docker run --rm -u "$(id -u):$(id -g)" -e HOME="$HOME" -v "$HOME:$HOME" \
		-v "$repo:$repo" -w "$repo" "$@"
}

case "${1:-run}" in
build)
	dock "$image" msbuild ChummerCS.sln -restore -p:Configuration=Debug -p:Platform=x86 -v:m -nologo
	;;
run)
	[ -f "$repo/Chummer/bin/Debug/Chummer.exe" ] || "$0" build
	# - a failed shift exits dash before || can catch it
	[ $# -gt 0 ] && shift
	file=""
	[ $# -gt 0 ] && file=$(realpath "$1")
	dock -e DISPLAY="${DISPLAY:-:0}" -v /tmp/.X11-unix:/tmp/.X11-unix "$image" \
		sh -c 'cd Chummer/bin/Debug && exec mono Chummer.exe "$@"' chummer ${file:+"$file"}
	;;
*)
	echo "usage: $0 build | run [file.chum]" >&2
	exit 2
	;;
esac
