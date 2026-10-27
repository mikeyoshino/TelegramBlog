const ASSETS={"videoPoster": "assets/videoPoster.jpg", "road": "assets/road.jpg", "lake": "assets/lake.jpg", "coffee": "assets/coffee.jpg", "sea": "assets/sea.jpg", "beach": "assets/beach.jpg", "interior": "assets/interior.jpg", "latte": "assets/latte.jpg", "art": "assets/art.jpg", "tokyo": "assets/tokyo.jpg", "hills": "assets/hills.jpg", "blue": "assets/blue.jpg", "cafe": "assets/cafe.jpg", "forest": "assets/forest.jpg", "flower": "assets/flower.jpg", "garden": "assets/garden.jpg", "snow": "assets/snow.jpg", "japan": "assets/japan.jpg", "alps": "assets/alps.jpg", "coast": "assets/coast.jpg", "waves": "assets/waves.jpg", "city": "assets/city.jpg", "mountain": "assets/mountain.jpg", "cloud": "assets/cloud.jpg", "island": "assets/island.jpg", "video": "assets/video.mp4"};
const groups=[{id:'travel',name:'Travel journal',thai:'เที่ยวด้วยกัน',topic:['ทะเลและเกาะ','ภูเขา','ญี่ปุ่น'],cover:['beach','japan','mountain'],desc:'เก็บเรื่องราวระหว่างทาง'},{id:'slow',name:'Slow Sunday',thai:'วันสบาย ๆ',topic:['คาเฟ่','ดอกไม้','แรงบันดาลใจ'],cover:['coffee','cafe','flower'],desc:'กาแฟดี ๆ กับวันธรรมดาที่พิเศษ'},{id:'ocean',name:'Ocean club',thai:'คนรักทะเล',topic:['มหาสมุทร','ชายฝั่ง'],cover:['waves','coast','sea'],desc:'สีฟ้าที่อยากกลับไปเจออีกครั้ง'}];
const raws=[
 ['beach','แสงเช้าที่ทะเล','travel','ทะเลและเกาะ',0,'album',['island','sea']],
 ['japan','บ่ายวันหนึ่งที่ญี่ปุ่น','travel','ญี่ปุ่น',0],
 ['coast','สีฟ้าในวันพักผ่อน','ocean','มหาสมุทร',0],
 ['coffee','กาแฟแก้วโปรด','slow','คาเฟ่',0],
 ['mountain','วิวระหว่างทาง','travel','ภูเขา',0,'album',['snow','hills']],
 ['cafe','มุมโปรดของวัน','slow','คาเฟ่',0],
 ['waves','เสียงของมหาสมุทร','ocean','มหาสมุทร',0],
 ['forest','เดินเล่นในป่า','travel','ภูเขา',0],
 ['flower','ฤดูของดอกไม้','slow','ดอกไม้',0],
 ['tokyo','หนึ่งคืนในโตเกียว','travel','ญี่ปุ่น',1],
 ['lake','สงบเหมือนผืนน้ำ','travel','ภูเขา',1],
 ['latte','เช้าวันอาทิตย์','slow','คาเฟ่',1],
 ['sea','เดินเล่นริมชายหาด','ocean','ชายฝั่ง',1,'album',['beach','coast']],
 ['videoPoster','ดอกไม้ในสายลม','slow','ดอกไม้',1,'video'],
 ['interior','พื้นที่เล็ก ๆ ที่อบอุ่น','slow','แรงบันดาลใจ',1],
 ['garden','แสงสุดท้ายของวัน','slow','ดอกไม้',1],
 ['island','เกาะในฝัน','travel','ทะเลและเกาะ',1],
 ['alps','ความเงียบของภูเขา','travel','ภูเขา',2],
 ['art','สีสันที่สะดุดตา','slow','แรงบันดาลใจ',2],
 ['road','ทางที่อยากเดินต่อ','travel','ภูเขา',2],
 ['snow','ลมหายใจของภูเขา','travel','ภูเขา',2],
 ['cloud','ฟ้าในวันนั้น','slow','แรงบันดาลใจ',2],
 ['hills','ขึ้นเขาไปด้วยกัน','travel','ภูเขา',2],
 ['blue','ภาพจำของทะเล','ocean','ชายฝั่ง',2]
];
const posts=raws.map((r,i)=>({id:i+1,asset:r[0],title:r[1],group:r[2],topic:r[3],day:r[4],type:r[5]||'photo',media:[r[0],...(r[6]||[])],tags:`${r[1]} ${r[3]} ${groups.find(g=>g.id===r[2]).name} ${groups.find(g=>g.id===r[2]).thai}`}));

export function previewData(){return {groups:groups.map(g=>({...g,id:g.id,title:g.name,topics:g.topic.map(t=>({id:t,title:t})),covers:g.cover.map(k=>ASSETS[k])})),posts:posts.map(p=>({...p,id:String(p.id),date:new Date(Date.UTC(2026,9,27-p.day,10)).toISOString(),media:p.media.map((key,i)=>({id:`${p.id}:${i}`,kind:p.type==="video"?"video":"image",thumb:ASSETS[key],url:p.type==="video"?ASSETS.video:ASSETS[key]}))}))};}
