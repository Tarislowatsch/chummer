#!/bin/sh
# Start Chummer on macOS: Mono from Homebrew, drawn through XQuartz.
#   start-macos.sh [file.chum]   start Chummer, optionally opening a character
#   start-macos.sh --setup       only check the requirements and build libgdiplus
set -eu

fail() {
	printf '%s\n' "$@" >&2
	exit 1
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

command -v brew >/dev/null 2>&1 || fail "Homebrew is missing, see https://brew.sh"
prefix=$(brew --prefix)
missing=""
command -v mono >/dev/null 2>&1 || missing="$missing mono"
for formula in mono-libgdiplus pkgconf libx11; do
	[ -d "$prefix/opt/$formula" ] || missing="$missing $formula"
done
[ -z "$missing" ] || fail "Some Homebrew packages are missing. Install them with:" "  brew install$missing"
[ -x /opt/X11/bin/Xquartz ] || fail "XQuartz is missing. Install it with:" \
	"  brew install --cask xquartz" "Then log out and back in once."
xcode-select -p >/dev/null 2>&1 || fail "The Xcode command line tools are missing. Install them with:" \
	"  xcode-select --install"

# - Homebrew builds libgdiplus --without-x11: Mono's X11 driver needs those calls
gdiplus="$HOME/.local/share/chummer/libgdiplus-x11"
if [ ! -f "$gdiplus/lib/libgdiplus.dylib" ]; then
	echo "Building libgdiplus with X11 support. This happens once and takes a few minutes."
	work=$(mktemp -d)
	curl -fsSL https://dl.winehq.org/mono/sources/libgdiplus/libgdiplus-6.2.tar.gz | tar xz -C "$work"
	(
		cd "$work/libgdiplus-6.2"
		export CPPFLAGS="-I$prefix/include" LDFLAGS="-L$prefix/lib"
		./configure --prefix="$gdiplus" > "$work/configure.log" 2>&1
		grep -q "X11 = yes" "$work/configure.log"
		make -j"$(sysctl -n hw.ncpu)" > "$work/make.log" 2>&1
		make install > /dev/null
	) || fail "Building libgdiplus failed. The logs are in $work"
	rm -rf "$work"
fi

[ "${1:-}" = --setup ] && exit 0

file=""
if [ $# -gt 0 ]; then
	file="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
fi

if [ -z "${DISPLAY:-}" ]; then
	open -a XQuartz
	export DISPLAY=:0
	sleep 3
fi

# - the X11 build of libgdiplus comes first, Homebrew's copy is on the fallback path too
export DYLD_LIBRARY_PATH="$gdiplus/lib"
export DYLD_FALLBACK_LIBRARY_PATH="$prefix/lib:/opt/X11/lib:/usr/local/lib:/usr/lib"
# - Mono's native macOS driver is 32-bit Carbon and fails on 64-bit macOS
export MONO_MWF_MAC_FORCE_X11=1
# - no XIM: the variadic XGetIMValues crashes under P/Invoke on Apple arm64
export MONO_WINFORMS_XIM_STYLE=disabled

cd "$app"
exec mono Chummer.exe ${file:+"$file"}
