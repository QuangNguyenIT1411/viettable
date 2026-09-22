import { useRef, useState } from "react";

type DialogKind = "confirm" | "prompt" | "alert";
type DialogVariant = "primary" | "danger";

type DialogOptions = {
  kind?: DialogKind;
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  defaultValue?: string;
  placeholder?: string;
  multiline?: boolean;
  variant?: DialogVariant;
};

type DialogState = Required<Pick<DialogOptions, "kind" | "title" | "message" | "confirmLabel" | "cancelLabel" | "variant">> &
  Pick<DialogOptions, "defaultValue" | "placeholder" | "multiline">;

export function useAppDialog() {
  const [dialog, setDialog] = useState<DialogState | null>(null);
  const [value, setValue] = useState("");
  const resolver = useRef<((value: boolean | string | null) => void) | null>(null);

  function open(options: DialogOptions) {
    setValue(options.defaultValue ?? "");
    setDialog({
      kind: options.kind ?? "confirm",
      title: options.title,
      message: options.message,
      confirmLabel: options.confirmLabel ?? "Đồng ý",
      cancelLabel: options.cancelLabel ?? "Hủy",
      defaultValue: options.defaultValue,
      placeholder: options.placeholder,
      multiline: options.multiline,
      variant: options.variant ?? "primary",
    });
  }

  function close(result: boolean | string | null) {
    const resolve = resolver.current;
    resolver.current = null;
    setDialog(null);
    setValue("");
    resolve?.(result);
  }

  function confirm(options: Omit<DialogOptions, "kind">) {
    return new Promise<boolean>((resolve) => {
      resolver.current = (result) => resolve(result === true);
      open({ ...options, kind: "confirm" });
    });
  }

  function prompt(options: Omit<DialogOptions, "kind">) {
    return new Promise<string | null>((resolve) => {
      resolver.current = (result) => resolve(typeof result === "string" ? result : null);
      open({ ...options, kind: "prompt" });
    });
  }

  function alert(options: Omit<DialogOptions, "kind" | "cancelLabel">) {
    return new Promise<void>((resolve) => {
      resolver.current = () => resolve();
      open({ ...options, kind: "alert", cancelLabel: "" });
    });
  }

  function Dialog() {
    if (!dialog) return null;
    const isPrompt = dialog.kind === "prompt";
    const isAlert = dialog.kind === "alert";
    const input = dialog.multiline ? (
      <textarea value={value} onChange={(event) => setValue(event.target.value)} placeholder={dialog.placeholder} rows={4} autoFocus />
    ) : (
      <input value={value} onChange={(event) => setValue(event.target.value)} placeholder={dialog.placeholder} autoFocus />
    );

    return (
      <div className="app-dialog-backdrop" role="presentation" onMouseDown={() => close(isAlert ? true : null)}>
        <section className="app-dialog" role="dialog" aria-modal="true" aria-labelledby="app-dialog-title" onMouseDown={(event) => event.stopPropagation()}>
          <h2 id="app-dialog-title">{dialog.title}</h2>
          <p>{dialog.message}</p>
          {isPrompt ? <div className="app-dialog-field">{input}</div> : null}
          <div className="app-dialog-actions">
            {!isAlert ? <button className="ghost" onClick={() => close(null)}>{dialog.cancelLabel}</button> : null}
            <button className={dialog.variant === "danger" ? "danger" : undefined} onClick={() => close(isPrompt ? value : true)}>
              {dialog.confirmLabel}
            </button>
          </div>
        </section>
      </div>
    );
  }

  return { confirm, prompt, alert, Dialog };
}
