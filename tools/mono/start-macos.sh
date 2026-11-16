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
if [ ! -f "$gdiplus/.complete" ]; then
	echo "Building libgdiplus with X11 support. This happens once and takes a few minutes."
	work=$(mktemp -d)
	tarball="$work/libgdiplus-6.2.tar.gz"
	curl -fsSL -o "$tarball" https://dl.winehq.org/mono/sources/libgdiplus/libgdiplus-6.2.tar.gz ||
		fail "Downloading libgdiplus failed."
	# - pinned to the hash Homebrew's mono-libgdiplus formula uses
	echo "683adb7d99d03f6ee7985173a206a2243f76632682334ced4cae2fcd20c83bc9  $tarball" |
		shasum -a 256 -c - > /dev/null || fail "The libgdiplus download has an unexpected checksum."
	tar xzf "$tarball" -C "$work" || fail "Unpacking libgdiplus failed."
	rm -rf "$gdiplus"
	# - an && chain: set -e does not apply inside a subshell left of ||
	(
		cd "$work/libgdiplus-6.2" &&
			CPPFLAGS="-I$prefix/include" LDFLAGS="-L$prefix/lib" \
				./configure --prefix="$gdiplus" > "$work/configure.log" 2>&1 &&
			grep -q "X11 = yes" "$work/configure.log" &&
			make -j"$(sysctl -n hw.ncpu)" > "$work/make.log" 2>&1 &&
			make install > /dev/null
	) || fail "Building libgdiplus failed. The logs are in $work"
	# - the marker comes last: a broken build has no .complete and is rebuilt
	touch "$gdiplus/.complete"
	rm -rf "$work"
fi

[ "${1:-}" = --setup ] && exit 0

file=""
if [ $# -gt 0 ]; then
	[ -f "$1" ] || fail "File not found: $1"
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
