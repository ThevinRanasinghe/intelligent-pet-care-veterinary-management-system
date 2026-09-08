/**
 * PetCare brand SVG illustration for the auth panel.
 * A friendly veterinary/pet themed illustration.
 */
export default function PetCareIllustration({ size = 220 }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 220 220"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      aria-hidden="true"
    >
      {/* Background circle */}
      <circle cx="110" cy="110" r="100" fill="rgba(17,17,17,0.06)" />

      {/* Dog body */}
      <ellipse cx="110" cy="130" rx="52" ry="44" fill="#FFFFFF" />

      {/* Dog head */}
      <circle cx="110" cy="85" r="38" fill="#FFFFFF" />

      {/* Ears */}
      <ellipse cx="82" cy="68" rx="16" ry="22" fill="#E8E0C8" transform="rotate(-15 82 68)" />
      <ellipse cx="138" cy="68" rx="16" ry="22" fill="#E8E0C8" transform="rotate(15 138 68)" />

      {/* Eyes */}
      <circle cx="97" cy="82" r="7" fill="#111111" />
      <circle cx="123" cy="82" r="7" fill="#111111" />
      <circle cx="99" cy="80" r="2.5" fill="white" />
      <circle cx="125" cy="80" r="2.5" fill="white" />

      {/* Nose */}
      <ellipse cx="110" cy="96" rx="7" ry="5" fill="#B8B49C" />
      <circle cx="108" cy="95" r="1.5" fill="white" />

      {/* Mouth smile */}
      <path d="M102 103 Q110 112 118 103" stroke="#B8B49C" strokeWidth="2.5" strokeLinecap="round" fill="none" />

      {/* Stethoscope */}
      <path d="M88 130 Q80 145 88 158 Q96 170 110 170 Q124 170 132 158 Q140 145 132 130"
        stroke="#FFBE00" strokeWidth="4" strokeLinecap="round" fill="none" />
      <circle cx="110" cy="172" r="8" fill="#FFBE00" />
      <circle cx="110" cy="172" r="4" fill="#111111" />

      {/* Paw print decorations */}
      <circle cx="170" cy="60" r="8" fill="rgba(17,17,17,0.1)" />
      <circle cx="162" cy="48" r="4" fill="rgba(17,17,17,0.08)" />
      <circle cx="174" cy="46" r="4" fill="rgba(17,17,17,0.08)" />
      <circle cx="182" cy="54" r="4" fill="rgba(17,17,17,0.08)" />

      <circle cx="45" cy="155" r="7" fill="rgba(17,17,17,0.08)" />
      <circle cx="38" cy="144" r="3.5" fill="rgba(17,17,17,0.06)" />
      <circle cx="49" cy="142" r="3.5" fill="rgba(17,17,17,0.06)" />
      <circle cx="56" cy="150" r="3.5" fill="rgba(17,17,17,0.06)" />

      {/* Heart */}
      <path d="M155 100 C155 96 150 92 146 95 C142 92 137 96 137 100 C137 106 146 114 146 114 C146 114 155 106 155 100Z"
        fill="#F26422" opacity="0.7" />
    </svg>
  );
}
