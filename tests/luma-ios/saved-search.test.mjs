import test from 'node:test';
import assert from 'node:assert/strict';
const search=await import('../../src/TeleGramBlog.iOS/Resources/Raw/wwwroot/saved-search.mjs').catch(()=>({}));
test('saved search matches words across group, topic, note and collection, ignoring case',()=>{
 assert.equal(typeof search.matchesSaved,'function');
 const fields=['บทเรียน Swift','กลุ่มนักพัฒนา','หน้าจอ Login','ทบทวน async await','เรียนวันหยุด'];
 assert.equal(search.matchesSaved(fields,'  SWIFT   วันหยุด '),true);
 assert.equal(search.matchesSaved(fields,'นักพัฒนา login'),true);
 assert.equal(search.matchesSaved(fields,'Swift missing'),false);
 assert.equal(search.matchesSaved(fields,''),true);
});
test('highlight escapes content and treats regex characters as literal search text',()=>{
 assert.equal(typeof search.highlightSaved,'function');
 assert.equal(search.highlightSaved('<img> C++ & [x]','C++ [x]'), '&lt;img&gt; <mark>C++</mark> &amp; <mark>[x]</mark>');
 assert.equal(search.highlightSaved('Swift swift','SWIFT'),'<mark>Swift</mark> <mark>swift</mark>');
});
test('excerpt reveals a match deep inside a long note',()=>{
 assert.equal(typeof search.searchExcerpt,'function');
 const excerpt=search.searchExcerpt('ต้นเรื่อง '.repeat(70)+'คำสำคัญ'+' ท้ายเรื่อง'.repeat(70),'คำสำคัญ');
 assert.ok(excerpt.includes('คำสำคัญ'));assert.ok(excerpt.length<220);assert.ok(excerpt.startsWith('…'));
});
