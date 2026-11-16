#!/bin/sh
# Start Chummer on Linux with the distribution's Mono.
#   start-linux.sh [file.chum]   start Chummer, optionally opening a character
#   start-linux.sh --setup       only check the requirements
set -eu

fail() {
	printf '%s\n' "$@" >&2
	exit 1
}

install_hint() {
	id=""
	if [ -r /etc/os-release ]; then
		id=$(. /etc/os-release && echo "${ID:-} ${ID_LIKE:-}")
	fi
	case "$id" in
	*debian* | *ubuntu*) echo "  sudo apt install mono-complete" ;;
	*fedora* | *rhel*) echo "  sudo dnf install mono-complete" ;;
	*suse*) echo "  sudo zypper install mono-complete" ;;
	*arch*) echo "  sudo pacman -S mono libgdiplus" ;;
	*) echo "  Mono with WinForms and libgdiplus, usually the package mono-complete" ;;
	esac
}

here=$(cd "$(dirname "$0")" && pwd)
app=""
if [ -f "$here/Chummer.exe" ]; then
	app=$here
else
	# - in the repository the newer build wins: chummer.sh builds Debug next to an old Release
	for dir in "$here/../../Chummer/bin/Debug" "$here/../../Chummer/bin/Release"; do
		[ -f "$dir/Chummer.exe" ] || continue
		if [ -z "$app" ] || [ "$dir/Chummer.exe" -nt "$app/Chummer.exe" ]; then
			app=$dir
		fi
	done
fi
[ -n "$app" ] || fail "Chummer.exe not found. Put this script next to Chummer.exe."
app=$(cd "$app" && pwd)

command -v mono >/dev/null 2>&1 || fail "Mono is missing. Install it with:" "$(install_hint)"
# - only a warning: a Mono outside the usual layout must still be able to start
mono=$(command -v mono)
resolved=$(readlink -f "$mono" 2> /dev/null) && [ -n "$resolved" ] && mono=$resolved
monolib="$(dirname "$mono")/../lib/mono"
[ -f "$monolib/4.5/System.Windows.Forms.dll" ] ||
	printf '%s\n' "Mono's WinForms was not found in $monolib. If Chummer fails, install it with:" \
		"$(install_hint)" >&2
[ -n "${DISPLAY:-}" ] || fail "DISPLAY is not set. Mono needs an X server, on Wayland that is XWayland."

[ "${1:-}" = --setup ] && exit 0

file=""
if [ $# -gt 0 ]; then
	[ -f "$1" ] || fail "File not found: $1"
	file="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
fi

cd "$app"
exec mono Chummer.exe ${file:+"$file"}
