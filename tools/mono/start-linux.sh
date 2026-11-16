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
for dir in "$here" "$here/../../Chummer/bin/Release" "$here/../../Chummer/bin/Debug"; do
	if [ -f "$dir/Chummer.exe" ]; then
		app=$(cd "$dir" && pwd)
		break
	fi
done
[ -n "$app" ] || fail "Chummer.exe not found. Put this script next to Chummer.exe."

command -v mono >/dev/null 2>&1 || fail "Mono is missing. Install it with:" "$(install_hint)"
monolib="$(dirname "$(command -v mono)")/../lib/mono"
[ -f "$monolib/4.5/System.Windows.Forms.dll" ] || fail "Mono's WinForms is missing. Install it with:" "$(install_hint)"
[ -n "${DISPLAY:-}" ] || fail "DISPLAY is not set. Mono needs an X server, on Wayland that is XWayland."

[ "${1:-}" = --setup ] && exit 0

file=""
if [ $# -gt 0 ]; then
	file="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
fi

cd "$app"
exec mono Chummer.exe ${file:+"$file"}
