"use client";

import { useState } from "react";
import { ApiClientError } from "@/lib/apiClient";
import { Button } from "@/shared/components/Button";
import { saveParcelToShortlist } from "../services/citizenWorkspaceApi";

export function SaveParcelButton({
  parcelId,
  variant = "secondary",
}: {
  parcelId: string;
  variant?: "primary" | "secondary";
}) {
  const [status, setStatus] = useState<"idle" | "saving" | "saved" | "error">(
    "idle",
  );
  const [message, setMessage] = useState<string | null>(null);

  async function handleSave() {
    if (status === "saving") {
      return;
    }
    setStatus("saving");
    setMessage(null);
    try {
      await saveParcelToShortlist(parcelId);
      setStatus("saved");
    } catch (err) {
      setStatus("error");
      if (err instanceof ApiClientError) {
        setMessage(err.detail || err.title);
      } else if (err instanceof Error) {
        setMessage(err.message);
      } else {
        setMessage("Could not save parcel.");
      }
    }
  }

  const label =
    status === "saved"
      ? "Saved to shortlist"
      : status === "saving"
        ? "Saving…"
        : "Save parcel";

  return (
    <span style={{ display: "inline-flex", flexDirection: "column", gap: 4 }}>
      <Button
        type="button"
        variant={variant}
        onClick={() => void handleSave()}
        disabled={status === "saving"}
      >
        {label}
      </Button>
      {message ? (
        <span style={{ fontSize: "0.75rem", color: "#850906" }} role="alert">
          {message}
        </span>
      ) : null}
    </span>
  );
}
