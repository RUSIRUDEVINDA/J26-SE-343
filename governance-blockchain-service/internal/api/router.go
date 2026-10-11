package api

import (
	"net/http"
	"strings"
)

// SetupRouter creates an HTTP handler with standard mux routing.
func SetupRouter(h *Handler) http.Handler {
	mux := http.NewServeMux()

	mux.HandleFunc("/health", h.HealthHandler)
	mux.HandleFunc("/api/v1/anchors", func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path == "/api/v1/anchors" || r.URL.Path == "/api/v1/anchors/" {
			h.PostAnchorHandler(w, r)
			return
		}
		// If path has subpath, route to AnchorByIDHandler
		h.AnchorByIDHandler(w, r)
	})
	mux.HandleFunc("/api/v1/anchors/", func(w http.ResponseWriter, r *http.Request) {
		trimmed := strings.TrimPrefix(r.URL.Path, "/api/v1/anchors/")
		if trimmed == "" {
			h.PostAnchorHandler(w, r)
			return
		}
		h.AnchorByIDHandler(w, r)
	})

	return mux
}
