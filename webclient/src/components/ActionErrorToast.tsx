export function ActionErrorToast({ message }: { message: string }) {
  return (
    <div
      style={{
        position: "fixed", left: "50%", bottom: 24, transform: "translateX(-50%)",
        maxWidth: "min(420px, calc(100vw - 32px))", padding: "10px 16px", borderRadius: 10,
        background: "rgba(127,29,29,.95)", color: "#fecaca", fontSize: 13, lineHeight: 1.4,
        boxShadow: "0 4px 16px rgba(0,0,0,.4)", zIndex: 1000,
      }}
    >
      {message}
    </div>
  );
}
