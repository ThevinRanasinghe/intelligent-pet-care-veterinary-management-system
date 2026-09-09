/**
 * Athletic Animal Mascot Team Illustration
 * Faithfully matches the Beacon Pet Health characters from the design reference:
 * - Center: Athletic Dog with yellow/black headband, floppy black ears, and orange "DM" jersey
 * - Left: Athletic Cat with headband, round eyes, yellow "D" shirt, and orange shorts
 * - Right: Athletic Bunny with headband, tall ears, yellow "M" shirt, and orange shorts
 * Includes smooth micro-animations for living, breathing characters.
 */
export default function PetCareIllustration({ size = 260, character = 'team' }) {
  // If a single character is requested (e.g. cat alone)
  if (character === 'cat') {
    return (
      <div className="mascot-container" style={{ width: size, height: size }}>
        <svg
          viewBox="0 0 160 160"
          width={size}
          height={size}
          fill="none"
          xmlns="http://www.w3.org/2000/svg"
          aria-hidden="true"
          className="mascot-animated-solo"
        >
          {/* Ground shadow */}
          <ellipse cx="80" cy="148" rx="42" ry="8" fill="#111111" opacity="0.15" />

          {/* Cat Body */}
          <g className="cat-group">
            {/* Tail */}
            <path
              d="M48 116 C30 110 24 88 34 82 C40 78 46 88 44 98"
              stroke="#111111"
              strokeWidth="4"
              strokeLinecap="round"
              fill="none"
              className="tail-animated"
            />

            {/* Legs & Socks */}
            <rect x="58" y="126" width="13" height="18" rx="4" fill="#3B9E8E" stroke="#111111" strokeWidth="3" />
            <line x1="58" y1="134" x2="71" y2="134" stroke="white" strokeWidth="2" />
            <path d="M55 142 C55 139 63 139 71 140 C75 141 75 146 68 146 L58 146 C55 146 55 144 55 142 Z" fill="white" stroke="#111111" strokeWidth="3" />

            <rect x="88" y="126" width="13" height="18" rx="4" fill="#3B9E8E" stroke="#111111" strokeWidth="3" />
            <line x1="88" y1="134" x2="101" y2="134" stroke="white" strokeWidth="2" />
            <path d="M85 142 C85 139 93 139 101 140 C105 141 105 146 98 146 L88 146 C85 146 85 144 85 142 Z" fill="white" stroke="#111111" strokeWidth="3" />

            {/* Orange Shorts */}
            <path d="M54 112 L106 112 L103 128 L84 128 L80 120 L76 128 L57 128 Z" fill="#FF6B2B" stroke="#111111" strokeWidth="3" strokeLinejoin="round" />

            {/* Yellow Shirt with 'D' */}
            <path d="M50 78 C50 74 60 72 80 72 C100 72 110 74 110 78 L108 114 L52 114 Z" fill="#FFC107" stroke="#111111" strokeWidth="3" strokeLinejoin="round" />
            {/* Sleeves */}
            <path d="M50 78 L38 92 C36 94 42 98 46 96 L54 86" fill="#FFC107" stroke="#111111" strokeWidth="3" strokeLinejoin="round" />
            <path d="M110 78 L122 92 C124 94 118 98 114 96 L106 86" fill="#FFC107" stroke="#111111" strokeWidth="3" strokeLinejoin="round" />

            {/* White paws */}
            <ellipse cx="38" cy="94" rx="5" ry="4" fill="white" stroke="#111111" strokeWidth="3" />
            <ellipse cx="122" cy="94" rx="5" ry="4" fill="white" stroke="#111111" strokeWidth="3" />

            {/* Letter 'D' on Shirt */}
            <text x="80" y="102" fontSize="22" fontWeight="900" fontFamily="sans-serif" textAnchor="middle" fill="#111111">D</text>

            {/* Cat Head */}
            <g className="head-bob">
              {/* Ears */}
              <path d="M50 36 L64 12 L74 30 Z" fill="white" stroke="#111111" strokeWidth="3" strokeLinejoin="round" />
              <path d="M56 32 L64 18 L70 28 Z" fill="#FFD54F" />

              <path d="M110 36 L96 12 L86 30 Z" fill="white" stroke="#111111" strokeWidth="3" strokeLinejoin="round" />
              <path d="M104 32 L96 18 L90 28 Z" fill="#FFD54F" />

              {/* Head Circle */}
              <circle cx="80" cy="46" r="32" fill="white" stroke="#111111" strokeWidth="3.5" />

              {/* Yellow & Black Athletic Headband */}
              <path d="M49 38 Q80 34 111 38 L111 47 Q80 43 49 47 Z" fill="#FFBE00" stroke="#111111" strokeWidth="3" />
              <path d="M49 41 Q80 37 111 41 L111 44 Q80 40 49 44 Z" fill="#111111" />

              {/* Eyes */}
              <circle cx="68" cy="52" r="7.5" fill="#111111" />
              <circle cx="66" cy="50" r="2.5" fill="white" />

              <circle cx="92" cy="52" r="7.5" fill="#111111" />
              <circle cx="90" cy="50" r="2.5" fill="white" />

              {/* Nose & Mouth */}
              <polygon points="80,59 77,56 83,56" fill="#111111" />
              <path d="M75 62 Q80 65 85 62" stroke="#111111" strokeWidth="2.5" strokeLinecap="round" fill="none" />
            </g>
          </g>
        </svg>
      </div>
    );
  }

  // Default: Team Trio (Cat, Dog, Bunny)
  return (
    <div className="mascot-container" style={{ width: size, height: size * 0.88 }}>
      <svg
        viewBox="0 0 320 280"
        width="100%"
        height="100%"
        fill="none"
        xmlns="http://www.w3.org/2000/svg"
        aria-hidden="true"
        className="mascot-team-svg"
      >
        {/* Ground shadow under all characters */}
        <ellipse cx="160" cy="254" rx="136" ry="16" fill="#111111" />

        {/* ═══════════════════════════════════════════════════════════ */}
        {/* 1. ATHLETIC CAT (LEFT)                                      */}
        {/* ═══════════════════════════════════════════════════════════ */}
        <g className="cat-mascot">
          {/* Tail */}
          <path
            d="M58 200 C34 194 28 162 42 154 C50 150 56 164 54 178"
            stroke="#111111"
            strokeWidth="4"
            strokeLinecap="round"
            fill="none"
            className="tail-animated"
          />

          {/* Left leg & Teal sock */}
          <rect x="74" y="218" width="14" height="22" rx="4" fill="#3B9E8E" stroke="#111111" strokeWidth="3.5" />
          <line x1="74" y1="228" x2="88" y2="228" stroke="white" strokeWidth="2.5" />
          <path d="M70 238 C70 234 80 234 88 235 C93 236 93 243 85 243 L74 243 C70 243 70 240 70 238 Z" fill="white" stroke="#111111" strokeWidth="3.5" />

          {/* Right leg & Teal sock */}
          <rect x="100" y="218" width="14" height="22" rx="4" fill="#3B9E8E" stroke="#111111" strokeWidth="3.5" />
          <line x1="100" y1="228" x2="114" y2="228" stroke="white" strokeWidth="2.5" />
          <path d="M96 238 C96 234 106 234 114 235 C119 236 119 243 111 243 L100 243 C96 243 96 240 96 238 Z" fill="white" stroke="#111111" strokeWidth="3.5" />

          {/* Orange Shorts */}
          <path d="M68 200 L120 200 L117 220 L98 220 L94 212 L90 220 L71 220 Z" fill="#FF6B2B" stroke="#111111" strokeWidth="3.5" strokeLinejoin="round" />

          {/* Yellow Shirt with 'D' */}
          <path d="M64 162 C64 157 75 155 94 155 C113 155 124 157 124 162 L121 202 L67 202 Z" fill="#FFC107" stroke="#111111" strokeWidth="3.5" strokeLinejoin="round" />
          <path d="M64 162 L50 176 C48 178 54 184 58 181 L68 170" fill="#FFC107" stroke="#111111" strokeWidth="3.5" strokeLinejoin="round" />
          <ellipse cx="49" cy="179" rx="5" ry="4" fill="white" stroke="#111111" strokeWidth="3.5" />

          {/* Letter 'D' */}
          <text x="94" y="190" fontSize="20" fontWeight="900" fontFamily="sans-serif" textAnchor="middle" fill="#111111">D</text>

          {/* Cat Head */}
          <g className="head-bob-slow">
            {/* Ears */}
            <path d="M66 122 L78 94 L90 114 Z" fill="white" stroke="#111111" strokeWidth="3.5" strokeLinejoin="round" />
            <path d="M72 118 L78 102 L84 114 Z" fill="#FFD54F" />

            <path d="M122 122 L110 94 L98 114 Z" fill="white" stroke="#111111" strokeWidth="3.5" strokeLinejoin="round" />
            <path d="M116 118 L110 102 L104 114 Z" fill="#FFD54F" />

            {/* Head Circle */}
            <circle cx="94" cy="132" r="30" fill="white" stroke="#111111" strokeWidth="3.5" />

            {/* Headband */}
            <path d="M65 124 Q94 120 123 124 L123 133 Q94 129 65 133 Z" fill="#FFBE00" stroke="#111111" strokeWidth="3.5" />
            <path d="M65 127 Q94 123 123 127 L123 130 Q94 126 65 130 Z" fill="#111111" />

            {/* Eyes */}
            <circle cx="83" cy="138" r="7" fill="#111111" />
            <circle cx="81" cy="136" r="2.5" fill="white" />

            <circle cx="105" cy="138" r="7" fill="#111111" />
            <circle cx="103" cy="136" r="2.5" fill="white" />

            {/* Snout */}
            <polygon points="94,144 91,141 97,141" fill="#111111" />
            <path d="M89 147 Q94 150 99 147" stroke="#111111" strokeWidth="2.5" strokeLinecap="round" fill="none" />
          </g>
        </g>

        {/* ═══════════════════════════════════════════════════════════ */}
        {/* 2. ATHLETIC DOG (CENTER - HERO)                             */}
        {/* ═══════════════════════════════════════════════════════════ */}
        <g className="dog-mascot">
          {/* Floppy Black Ears (Behind Head) */}
          <ellipse cx="140" cy="116" rx="16" ry="32" fill="#111111" transform="rotate(-15 140 116)" />
          <ellipse cx="216" cy="116" rx="16" ry="32" fill="#111111" transform="rotate(15 216 116)" />

          {/* Legs & Teal Leg Warmers */}
          <rect x="156" y="200" width="18" height="34" rx="5" fill="#3B9E8E" stroke="#111111" strokeWidth="4" />
          <line x1="156" y1="214" x2="174" y2="214" stroke="white" strokeWidth="3" />
          <line x1="156" y1="222" x2="174" y2="222" stroke="white" strokeWidth="3" />
          <path d="M152 232 C152 226 164 226 174 227 C180 229 180 240 170 240 L156 240 C152 240 152 236 152 232 Z" fill="white" stroke="#111111" strokeWidth="4" />

          <rect x="186" y="200" width="18" height="34" rx="5" fill="#3B9E8E" stroke="#111111" strokeWidth="4" />
          <line x1="186" y1="214" x2="204" y2="214" stroke="white" strokeWidth="3" />
          <line x1="186" y1="222" x2="204" y2="222" stroke="white" strokeWidth="3" />
          <path d="M182 232 C182 226 194 226 204 227 C210 229 210 240 200 240 L186 240 C182 240 182 236 182 232 Z" fill="white" stroke="#111111" strokeWidth="4" />

          {/* Orange Athletic Jersey with 'DM' */}
          <path d="M144 142 C144 135 158 132 178 132 C198 132 212 135 212 142 L208 206 L148 206 Z" fill="#FF6B2B" stroke="#111111" strokeWidth="4" strokeLinejoin="round" />

          {/* Jersey Sleeves & White Paws */}
          <path d="M144 142 L128 160 C125 163 132 170 137 167 L148 152" fill="#FF6B2B" stroke="#111111" strokeWidth="4" strokeLinejoin="round" />
          <circle cx="127" cy="164" r="6" fill="white" stroke="#111111" strokeWidth="4" />

          <path d="M212 142 L228 160 C231 163 224 170 219 167 L208 152" fill="#FF6B2B" stroke="#111111" strokeWidth="4" strokeLinejoin="round" />
          <circle cx="229" cy="164" r="6" fill="white" stroke="#111111" strokeWidth="4" />

          {/* Letters 'DM' */}
          <text x="178" y="176" fontSize="24" fontWeight="900" fontFamily="sans-serif" textAnchor="middle" fill="#111111">DM</text>

          {/* Dog Head */}
          <g className="head-bob-hero">
            {/* White Head */}
            <circle cx="178" cy="106" r="38" fill="white" stroke="#111111" strokeWidth="4" />

            {/* Yellow & Black Headband */}
            <path d="M142 94 Q178 88 214 94 L214 105 Q178 99 142 105 Z" fill="#FFBE00" stroke="#111111" strokeWidth="4" />
            <path d="M142 98 Q178 92 214 98 L214 101 Q178 95 142 101 Z" fill="#111111" />

            {/* Big Expressive Cartoon Eyes */}
            <circle cx="164" cy="112" r="9" fill="#111111" />
            <circle cx="161" cy="109" r="3.2" fill="white" />

            <circle cx="192" cy="112" r="9" fill="#111111" />
            <circle cx="189" cy="109" r="3.2" fill="white" />

            {/* Black Snout / Nose */}
            <ellipse cx="178" cy="120" rx="9" ry="6" fill="#111111" />
            <circle cx="176" cy="118" r="1.5" fill="white" />

            {/* Smile */}
            <path d="M171 127 Q178 133 185 127" stroke="#111111" strokeWidth="3" strokeLinecap="round" fill="none" />
          </g>
        </g>

        {/* ═══════════════════════════════════════════════════════════ */}
        {/* 3. ATHLETIC BUNNY (RIGHT)                                   */}
        {/* ═══════════════════════════════════════════════════════════ */}
        <g className="bunny-mascot">
          {/* Bunny Ears */}
          <ellipse cx="238" cy="134" rx="7" ry="24" fill="white" stroke="#111111" strokeWidth="3" transform="rotate(-6 238 134)" />
          <ellipse cx="238" cy="134" rx="3.5" ry="16" fill="#FFD54F" transform="rotate(-6 238 134)" />

          <ellipse cx="258" cy="136" rx="7" ry="24" fill="white" stroke="#111111" strokeWidth="3" transform="rotate(8 258 136)" />
          <ellipse cx="258" cy="136" rx="3.5" ry="16" fill="#FFD54F" transform="rotate(8 258 136)" />

          {/* Legs */}
          <rect x="238" y="224" width="10" height="18" rx="3" fill="#3B9E8E" stroke="#111111" strokeWidth="3" />
          <path d="M235 238 C235 234 242 234 248 235 C252 236 252 242 246 242 L238 242 C235 242 235 240 235 238 Z" fill="white" stroke="#111111" strokeWidth="3" />

          <rect x="254" y="224" width="10" height="18" rx="3" fill="#3B9E8E" stroke="#111111" strokeWidth="3" />
          <path d="M251 238 C251 234 258 234 264 235 C268 236 268 242 262 242 L254 242 C251 242 251 240 251 238 Z" fill="white" stroke="#111111" strokeWidth="3" />

          {/* Orange Shorts */}
          <path d="M232 208 L268 208 L266 226 L252 226 L250 220 L248 226 L234 226 Z" fill="#FF6B2B" stroke="#111111" strokeWidth="3" strokeLinejoin="round" />

          {/* Yellow Shirt with 'M' */}
          <path d="M230 182 C230 178 238 176 250 176 C262 176 270 178 270 182 L268 210 L232 210 Z" fill="#FFC107" stroke="#111111" strokeWidth="3" strokeLinejoin="round" />
          <text x="250" y="202" fontSize="16" fontWeight="900" fontFamily="sans-serif" textAnchor="middle" fill="#111111">M</text>

          {/* Bunny Head */}
          <g className="head-bob-fast">
            <circle cx="248" cy="162" r="21" fill="white" stroke="#111111" strokeWidth="3" />

            {/* Headband */}
            <path d="M228 156 Q248 153 268 156 L268 163 Q248 160 228 163 Z" fill="#FFBE00" stroke="#111111" strokeWidth="2.5" />
            <path d="M228 158 Q248 155 268 158 L268 161 Q248 158 228 161 Z" fill="#111111" />

            {/* Eyes */}
            <circle cx="240" cy="166" r="5" fill="#111111" />
            <circle cx="238" cy="164" r="1.8" fill="white" />

            <circle cx="256" cy="166" r="5" fill="#111111" />
            <circle cx="254" cy="164" r="1.8" fill="white" />

            {/* Bunny Nose & Mouth */}
            <polygon points="248,171 246,169 250,169" fill="#111111" />
            <path d="M245 173 Q248 175 251 173" stroke="#111111" strokeWidth="2" strokeLinecap="round" fill="none" />
          </g>
        </g>
      </svg>
    </div>
  );
}
