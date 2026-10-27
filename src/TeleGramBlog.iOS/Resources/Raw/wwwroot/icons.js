const ICONS={
 refresh:'<path d="M20 7v5h-5M4 17v-5h5"/><path d="M6.1 7a7 7 0 0 1 11.6-1L20 9M4 15l2.3 3A7 7 0 0 0 17.9 17"/>',
 luma:'<path d="M6 5H13.7C17.9 5 19.5 9.6 16.7 11.7C20.4 13.2 20.2 18.1 16.7 19.4V22L13.8 19.6H6C5.1 19.6 4.6 19.1 4.6 18.2V6.4C4.6 5.5 5.1 5 6 5Z M8.1 7.8V12.1C8.6 11.1 9.2 10.9 10.2 10.9H13.6C15.9 10.9 15.9 7.8 13.6 7.8Z M8.1 14.1V16.8H14.2C16.6 16.8 16.6 13.4 14.2 13.4H8.8C8.3 13.4 8.1 13.6 8.1 14.1Z M5.25 12.1L6.3 11.05L7.35 12.1V13.85L6.3 13.05L5.25 13.85Z" fill="currentColor" fill-rule="evenodd" stroke="none" transform="translate(0 -1)"/>',
 grid:'<rect x="3.5" y="3.5" width="6.5" height="6.5" rx="1.8"/><rect x="14" y="3.5" width="6.5" height="6.5" rx="1.8"/><rect x="3.5" y="14" width="6.5" height="6.5" rx="1.8"/><rect x="14" y="14" width="6.5" height="6.5" rx="1.8"/>',
 albums:'<rect x="4" y="6" width="16" height="15" rx="3"/><path d="M7 3h10M8 10h8M8 14h5"/>',
 heart:'<path d="M20.8 5.6a5.2 5.2 0 0 0-7.4 0L12 7l-1.4-1.4a5.2 5.2 0 0 0-7.4 7.3L12 21l8.8-8.1a5.2 5.2 0 0 0 0-7.3Z"/>',
 search:'<circle cx="10.5" cy="10.5" r="6.7"/><path d="m16 16 4.5 4.5"/>',
 sliders:'<path d="M4 6h16M4 12h16M4 18h16"/><circle cx="9" cy="6" r="2" fill="currentColor" stroke="none"/><circle cx="15" cy="12" r="2" fill="currentColor" stroke="none"/><circle cx="8" cy="18" r="2" fill="currentColor" stroke="none"/>',
 close:'<path d="m6 6 12 12M18 6 6 18"/>',closeCircle:'<circle cx="12" cy="12" r="9"/><path d="m9 9 6 6m0-6-6 6"/>',
 chevronDown:'<path d="m7 10 5 5 5-5"/>',chevronRight:'<path d="m9 5 7 7-7 7"/>',chevronLeft:'<path d="m15 5-7 7 7 7"/>',arrowLeft:'<path d="M20 12H4m6-6-6 6 6 6"/>',
 photo:'<rect x="3" y="3" width="18" height="18" rx="4"/><circle cx="8" cy="8" r="1.3"/><path d="m3 17 5-5 4 4 4-6 5 7"/>',
 play:'<path d="m8 5 11 7-11 7Z" fill="currentColor" stroke="none"/>',
 stack:'<rect x="7" y="3" width="14" height="14" rx="3"/><path d="M17 21H6a3 3 0 0 1-3-3V7"/>',
 check:'<path d="m5 12 4.5 4.5L19 7"/>',checkCircle:'<circle cx="12" cy="12" r="9"/><path d="m8 12 3 3 5-6"/>',
 info:'<circle cx="12" cy="12" r="9"/><path d="M12 11v6M12 7h.01"/>',
 download:'<path d="M12 3v12m-5-5 5 5 5-5M4 16v4a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1v-4"/>',
 location:'<path d="m21 3-7 18-4-7-7-4Z"/><path d="m10 14 11-11"/>',
 expand:'<path d="M8 3H3v5m13-5h5v5M3 16v5h5m13-5v5h-5"/>',collapse:'<path d="M3 8h5V3m13 5h-5V3M8 21v-5H3m13 5v-5h5"/>',
 sort:'<path d="M5 4v16m-3-3 3 3 3-3M12 5h9m-9 7h6m-6 7h3"/>',folder:'<path d="M3 7V5a2 2 0 0 1 2-2h5l2 3h7a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2Z"/>',
 signal:'<path d="M4 18v-3m5 3v-6m5 6V8m5 10V4" stroke-width="3"/>',wifi:'<path d="M2 8a16 16 0 0 1 20 0M5 12a11 11 0 0 1 14 0m-10 4a5 5 0 0 1 6 0M12 20h.01"/>',battery:'<rect x="2" y="6" width="18" height="12" rx="3"/><path d="M23 10v4"/><rect x="5" y="9" width="12" height="6" rx="1" fill="currentColor" stroke="none"/>',spark:'<path d="m12 2 2.5 7.5L22 12l-7.5 2.5L12 22l-2.5-7.5L2 12l7.5-2.5Z"/>'
};
const icon=(name,extra='')=>`<svg viewBox="0 0 24 24" aria-hidden="true" ${extra}>${ICONS[name]||ICONS.photo}</svg>`;
const $=id=>document.getElementById(id);
