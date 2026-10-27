const text=value=>String(value??'').normalize('NFC');
const escape=value=>text(value).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const terms=query=>[...new Set(text(query).trim().split(/\s+/u).filter(Boolean))];
const literal=value=>value.replace(/[.*+?^${}()|[\]\\]/g,'\\$&');
const pattern=query=>{const words=terms(query).sort((a,b)=>b.length-a.length);return words.length?new RegExp(words.map(literal).join('|'),'giu'):null};
export function matchesSaved(fields,query){const haystack=fields.map(text).join('\n');return terms(query).every(word=>new RegExp(literal(word),'iu').test(haystack))}
export function highlightSaved(value,query){
 const source=text(value),re=pattern(query);if(!re)return escape(source);
 let result='',offset=0;for(const match of source.matchAll(re)){result+=escape(source.slice(offset,match.index))+'<mark>'+escape(match[0])+'</mark>';offset=match.index+match[0].length}return result+escape(source.slice(offset));
}
export function searchExcerpt(value,query){
 const source=text(value),match=pattern(query)?.exec(source),start=Math.max(0,(match?.index??0)-45),end=Math.min(source.length,start+170);
 return (start?'…':'')+source.slice(start,end)+(end<source.length?'…':'');
}
export function firstMatchingField(fields,query){const re=pattern(query);return fields.find(field=>{if(!re)return false;re.lastIndex=0;return re.test(text(field))})||fields.find(Boolean)||''}
