// CanvasStamp - removed.
//
// This drew a small "Protected - <owner> - N locked" mark in the corner of the
// canvas. It was built, then defaulted to off, and is now gone entirely.
//
// The reasoning, kept because it is a design decision rather than a bug: the
// canvas belongs to whoever is working in it, not to the plugin. Other
// Grasshopper plugins put permanent overlays there and people read them as an
// obstruction - which is a reputation GHShield would inherit whether or not it
// deserved it. The GHShield component's panel carries exactly the same
// information, on request, inside the document, where the reader can move it,
// resize it or delete it.
//
// This file is intentionally empty so it compiles. It can be deleted from the
// project whenever convenient.
